using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
#if BuildMaster
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Documentation;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.Files
{
    [DisplayName("Transfer Files")]
    [Description("Copies files from a directory on a source server to a directory on a target server.")]
    [ScriptAlias("Transfer-Files")]
    [ScriptNamespace("Files", PreferUnqualified = true)]
    [Tag(Tags.Files)]
    public sealed class TransferFilesOperation : ExecuteOperation
    {
        private long totalBytes;
        private long bytesCopied;

        [ScriptAlias("Include")]
        [PlaceholderText("* (top-level items)")]
#if Hedgehog
        [MaskingDescription]
#else
        [Description(CommonDescriptions.MaskingHelp)]
#endif
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
#if Hedgehog
        [MaskingDescription]
#else
        [Description(CommonDescriptions.MaskingHelp)]
#endif
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("FromDirectory")]
        [DisplayName("Source directory")]
        [PlaceholderText("$WorkingDirectory")]
        public string SourceDirectory { get; set; }
        [ScriptAlias("FromServer")]
        [DisplayName("Source server")]
        [PlaceholderText("$ServerName")]
        public string SourceServerName { get; set; }
        [Required]
        [ScriptAlias("ToDirectory")]
        [DisplayName("Target directory")]
        public string TargetDirectory { get; set; }
        [ScriptAlias("ToServer")]
        [DisplayName("Target server")]
        [PlaceholderText("Same as source server")]
        public string TargetServerName { get; set; }
        [ScriptAlias("DeleteTarget")]
        [DisplayName("Delete target")]
        [Description("When set to true, files in the target directory will be deleted if they are not present in the source directory. "
                   + "If false, files present in the target directory that are not present in the source directory are unmodified.")]
        public bool DeleteTarget { get; set; }

        [Category("Advanced")]
        [ScriptAlias("SetLastModifiedDate")]
        [DisplayName("Set last modified")]
        [Description("When true, the modified date on any transferred files will be set to match their source files.")]
        [DefaultValue(true)]
        public bool SetLastModifiedDate { get; set; } = true;

        [Category("Advanced")]
        [ScriptAlias("BatchSize")]
        [DisplayName("Batch size")]
        [Description("The number of files to transfer in each batch.")]
        [DefaultValue(1)]
        public int BatchSize { get; set; } = 1;

        [Category("Advanced")]
        [ScriptAlias("Verbose")]
        [DisplayName("Verbose")]
        public bool VerboseLogging { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(this.SourceServerName) && string.IsNullOrWhiteSpace(this.SourceDirectory))
            {
                this.LogError("If SourceServer is specified, SourceDirectory must also be specified.");
                return;
            }

            var sourceAgent = context.Agent;
            var targetAgent = context.Agent;
            if (!string.IsNullOrWhiteSpace(this.SourceServerName))
            {
                this.LogDebug($"Attempting to resolve source server \"{this.SourceServerName}\"...");
                sourceAgent = await context.GetAgentAsync(this.SourceServerName).ConfigureAwait(false);
            }
            else
            {
                this.LogDebug("Using default source server.");
            }

            if (!string.IsNullOrWhiteSpace(this.TargetServerName))
            {
                this.LogDebug($"Attempting to resolve target server \"{this.TargetServerName}\"...");
                targetAgent = await context.GetAgentAsync(this.TargetServerName).ConfigureAwait(false);
            }
            else
            {
                this.LogDebug("Using default target server.");
            }

            if (sourceAgent == null)
            {
                this.LogError("Source server was not specified and there is no server in the current context.");
                return;
            }

            if (targetAgent == null)
            {
                this.LogError("Target server was not specified and there is no server in the current context.");
                return;
            }

            var sourceFileOps = await sourceAgent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
            var targetFileOps = await targetAgent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var sourceDirectory = this.SourceDirectory ?? context.WorkingDirectory;
#if BuildMaster
            if (sourceDirectory.StartsWith("~\\") || sourceDirectory.StartsWith("~/"))
                sourceDirectory = sourceFileOps.CombinePath(sourceFileOps.GetExecutionBaseDirectory(context), sourceDirectory.Substring(2));
#endif

            this.LogDebug("Source directory: " + sourceDirectory);
            this.LogDebug("Getting source file list...");
            var sourceItems = await sourceFileOps.GetFileSystemInfosAsync(sourceDirectory, new MaskingContext(this.Includes, this.Excludes)).ConfigureAwait(false);

            var targetDirectory = this.TargetDirectory ?? context.WorkingDirectory;
#if BuildMaster
            if (targetDirectory.StartsWith("~\\") || targetDirectory.StartsWith("~/"))
                targetDirectory = targetFileOps.CombinePath(targetFileOps.GetExecutionBaseDirectory(context), targetDirectory.Substring(2));
#endif

            this.LogDebug("Target directory: " + targetDirectory);
            if (!PathEx.IsPathRooted(targetDirectory))
            {
                this.LogError("Target directory must be rooted.");
                return;
            }

            if (this.VerboseLogging)
                this.LogDebug($"Ensuring that {targetDirectory} exists...");
            await targetFileOps.CreateDirectoryAsync(targetDirectory);

            this.LogDebug("Getting target file list...");
            var targetItems = targetFileOps
                .GetFileSystemInfos(targetDirectory, new MaskingContext(this.Includes, this.Excludes))
                .ToDictionary(f => f.FullName.Replace('\\', '/'));

            var sourcePath = sourceDirectory.TrimEnd('/', '\\');
            var targetPath = targetDirectory;

            int filesCopied = 0;
            int directoriesCopied = 0;
            int filesDeleted = 0;
            int directoriesDeleted = 0;

            var sourceFiles = sourceItems.OfType<SlimFileInfo>().ToList();
            var sourceDirs = sourceItems.OfType<SlimDirectoryInfo>().ToList();
            Interlocked.Exchange(ref this.totalBytes, sourceFiles.Sum(f => f.Size));

            Func<SlimFileInfo, Task> transferFile = async (file) =>
            {
                var targetFileName = PathEx.Combine(targetPath, file.FullName.Substring(sourcePath.Length).TrimStart('/', '\\')).Replace(sourceFileOps.DirectorySeparator, targetFileOps.DirectorySeparator);
                if (this.VerboseLogging)
                    this.LogDebug($"Copying {file.FullName} to {targetFileName}...");

                try
                {
                    await this.TransferFileAsync(sourceFileOps, file, targetFileOps, targetItems.GetValueOrDefault(targetFileName.Replace('\\', '/')), PathEx.GetDirectoryName(targetFileName)).ConfigureAwait(false);
                    Interlocked.Increment(ref filesCopied);
                }
                catch (Exception ex)
                {
                    this.LogError($"Cannot copy {file.FullName}: {ex.Message}");
                }

                Interlocked.Add(ref this.bytesCopied, file.Size);
            };

            int batches = sourceFiles.Count / this.BatchSize;
            for (int batch = 0; batch < batches; batch++)
            {
                var tasks = new Task[this.BatchSize];
                for (int i = 0; i < this.BatchSize; i++)
                {
                    var file = sourceFiles[batch * this.BatchSize + i];
                    tasks[i] = transferFile(file);
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            if (batches * this.BatchSize != sourceFiles.Count)
            {
                var remaining = new Task[sourceFiles.Count % this.BatchSize];
                for (int i = batches * this.BatchSize; i < sourceFiles.Count; i++)
                {
                    var file = sourceFiles[i];
                    remaining[i % this.BatchSize] = transferFile(file);
                }
                await Task.WhenAll(remaining).ConfigureAwait(false);
            }

            foreach (var dir in sourceDirs)
            {
                var targetDir = PathEx.Combine(targetPath, dir.FullName.Substring(sourcePath.Length).TrimStart('/', '\\')).Replace(sourceFileOps.DirectorySeparator, targetFileOps.DirectorySeparator);
                if (this.VerboseLogging)
                    this.LogDebug($"Creating directory {targetDir}...");

                await targetFileOps.CreateDirectoryAsync(targetDir).ConfigureAwait(false);
                directoriesCopied++;
            }

            if (this.DeleteTarget)
            {
                var sourceItems2 = sourceItems.Select(f => f.FullName.Substring(sourcePath.Length).TrimStart('/', '\\').Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var target in targetItems.Values)
                {
                    var relativeItemPath = target.FullName.Substring(targetPath.Length).TrimStart('/', '\\');
                    if (!sourceItems2.Contains(relativeItemPath.Replace('\\', '/')))
                    {
                        if (target is SlimFileInfo)
                        {
                            if (this.VerboseLogging)
                                this.LogDebug($"Deleting {target.FullName}...");

                            await targetFileOps.DeleteFileAsync(target.FullName).ConfigureAwait(false);
                            filesDeleted++;
                        }
                        else
                        {
                            if (this.VerboseLogging)
                                this.LogDebug($"Deleting directory {target.FullName}...");

                            await targetFileOps.DeleteDirectoriesAsync(new[] { target.FullName }).ConfigureAwait(false);
                            directoriesDeleted++;
                        }
                    }
                }
            }

            this.LogDebug($"Copied {filesCopied} files, deleted {filesDeleted} files and {directoriesDeleted} directories over {directoriesCopied} directories.");
        }

        public override OperationProgress GetProgress()
        {
            long total = Interlocked.Read(ref this.totalBytes);
            long copied = Interlocked.Read(ref this.bytesCopied);

            if (total == 0)
                return null;

            return new OperationProgress(
                (int)(100.0 * copied / total),
                AH.FormatSize(total - copied) + " remaining"
            );
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    ParseBool(config[nameof(this.DeleteTarget)]) ? "Mirror " : "Transfer ",
                    new MaskHilite(config[nameof(this.Includes)], config[nameof(this.Excludes)])
                ),
                new RichDescription(
                    "from ",
                    new Hilite(AH.CoalesceString(config[nameof(this.SourceServerName)], "$Server")),
                    "::",
                    new DirectoryHilite(config[nameof(this.SourceDirectory)]),
                    " to ",
                    new Hilite(AH.CoalesceString(config[nameof(this.TargetServerName)], "$Server")),
                    "::",
                    new DirectoryHilite(config[nameof(this.TargetDirectory)])
                )
            );
        }

        private static bool ParseBool(string s)
        {
            bool b;
            bool.TryParse(s, out b);
            return b;
        }
        private async Task TransferFileAsync(IFileOperationsExecuter sourceFileOps, SlimFileInfo sourceFile, IFileOperationsExecuter targetFileOps, SlimFileSystemInfo target, string targetDirectory)
        {
            if (target == null)
            {
                if (this.VerboseLogging)
                    this.LogDebug($"{sourceFile.Name} does not exist in {targetDirectory}.");
            }

            var targetFile = target as SlimFileInfo;
            if (targetFile != null)
            {
                if (this.VerboseLogging)
                {
                    this.LogDebug($"{sourceFile.Name} already exists in {targetDirectory}.");

                    this.LogDebug($"Source timestamp: {sourceFile.LastWriteTimeUtc}, Target timestamp: {targetFile.LastWriteTimeUtc}");
                    this.LogDebug($"Source size: {sourceFile.Size}, Target size: {targetFile.Size}");
                }
                if (sourceFile.LastWriteTimeUtc == targetFile.LastWriteTimeUtc && sourceFile.Size == targetFile.Size)
                {
                    if (this.VerboseLogging)
                        this.LogDebug($"Size and timestamp are the same; skipping {sourceFile.Name}...");
                    return;
                }
            }
            else if (target != null)
            {
                this.LogDebug($"{sourceFile.Name} is a file in {sourceFile.DirectoryName}, but a directory in {targetDirectory}.");

                if (this.VerboseLogging)
                    this.LogDebug($"Deleting directory {target.FullName}...");

                await targetFileOps.DeleteDirectoryAsync(target.FullName).ConfigureAwait(false);
            }
            else
            {
                if (!string.IsNullOrEmpty(targetDirectory))
                    await targetFileOps.CreateDirectoryAsync(targetDirectory).ConfigureAwait(false);
            }

            var targetFileName = PathEx.Combine(targetDirectory, sourceFile.Name).Replace(sourceFileOps.DirectorySeparator, targetFileOps.DirectorySeparator);

            if (this.VerboseLogging)
                this.LogDebug($"Transferring {sourceFile.Name} to {targetDirectory}...");

            using (var sourceStream = await sourceFileOps.OpenFileAsync(sourceFile.FullName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
            using (var targetStream = await targetFileOps.OpenFileAsync(targetFileName, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
            {
                await sourceStream.CopyToAsync(targetStream).ConfigureAwait(false);
            }

            if (this.SetLastModifiedDate)
                await targetFileOps.SetLastWriteTimeAsync(targetFileName, sourceFile.LastWriteTimeUtc).ConfigureAwait(false);
        }

        private sealed class DirectoryToTransfer
        {
            public DirectoryToTransfer(string sourceDirectory, string targetDirectory)
            {
                this.SourceDirectory = sourceDirectory;
                this.TargetDirectory = targetDirectory;
            }

            public string SourceDirectory { get; }
            public string TargetDirectory { get; }
        }
    }
}
