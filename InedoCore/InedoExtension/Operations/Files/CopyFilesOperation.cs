using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.Operations.Files
{
    [Description("Copies files on a server.")]
    [ScriptAlias("Copy-Files")]
    [ScriptNamespace(Namespaces.Files, PreferUnqualified = true)]
    [Example(@"
# copy all files and all subdirectories beneath it to the target,
# and log each individual file that is copied, and overwrite any files
Copy-Files(
    From: E:\Source,
    To: F:\Target,
    Include: **,
    Verbose: true,
    Overwrite: true
);
")]
    [Example(@"
# copy a file, renaming it during the copy
Copy-Files(
    From: $WorkingDirectory/build,
    To: $WorkingDirectory/staging,
    Include: HDARS.exe,
    Verbose: true,
    RenameFrom: .exe,
    RenameTo: .$ReleaseNumber.exe
);
")]
    [Example(@"
# copy files, renaming them to fit the format desired by this application
Copy-Files(
    From: vendorData,
    To: vendorData2,
    Verbose: true,
    RenameFrom: ""^(?<module>[a-z]+)\.(?<name>.*)\.xml$"",
    RenameTo: `${name}-`${module}.xml,
    RenameRegex: true
);
")]
    public sealed class CopyFilesOperation : ExecuteOperation
    {
        private int filesCopied;
        private int directoriesCopied;
        private Func<string, string> renameFile;

        [ScriptAlias("Include")]
        [MaskingDescription]
        [PlaceholderText("* (top-level items)")]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("From")]
        [PlaceholderText("$WorkingDirectory")]
        [DisplayName("Source directory")]
        public string SourceDirectory { get; set; }
        [Required]
        [ScriptAlias("To")]
        [DisplayName("Target directory")]
        public string TargetDirectory { get; set; }
        [ScriptAlias("Verbose")]
        [DisplayName("Verbose")]
        public bool VerboseLogging { get; set; }
        [ScriptAlias("Overwrite")]
        [DisplayName("Overwrite target files")]
        public bool Overwrite { get; set; }
        [Category("Rename")]
        [ScriptAlias("RenameFrom")]
        [DisplayName("Rename from")]
        public string RenameFrom { get; set; }
        [Category("Rename")]
        [ScriptAlias("RenameTo")]
        [DisplayName("Rename to")]
        public string RenameTo { get; set; }
        [Category("Rename")]
        [ScriptAlias("RenameRegex")]
        [DisplayName("Use regular expression")]
        [DefaultValue(false)]
        public bool RenameRegex { get; set; }

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrEmpty(this.RenameFrom) && !string.IsNullOrEmpty(this.RenameTo))
                throw new InvalidOperationException("If RenameTo is set, RenameFrom must also be set.");

            var sourceDirectory = context.ResolvePath(this.SourceDirectory);
            var targetDirectory = context.ResolvePath(this.TargetDirectory);

            if (!string.IsNullOrEmpty(this.RenameFrom))
            {
                var renameFrom = new Regex(this.RenameRegex ? this.RenameFrom : Regex.Escape(this.RenameFrom), RegexOptions.Singleline | RegexOptions.CultureInvariant);
                var renameTo = this.RenameTo ?? string.Empty;
                if (!this.RenameRegex)
                    renameTo = renameTo.Replace("$", "$$");
                this.renameFile = name => renameFrom.Replace(name, renameTo);
            }

            this.LogInformation($"Copying files from {sourceDirectory} to {targetDirectory}...");
            return this.CopyDirectoryAsync(context.Agent.GetService<IFileOperationsExecuter>(), sourceDirectory, targetDirectory, context);
        }

        private async Task CopyDirectoryAsync(IFileOperationsExecuter fileOps, string sourcePath, string targetPath, IOperationExecutionContext context)
        {
            if (!await fileOps.DirectoryExistsAsync(sourcePath).ConfigureAwait(false))
            {
                this.LogWarning($"Source directory {sourcePath} does not exist.");
                return;
            }

            var infos = await fileOps.GetFileSystemInfosAsync(
                sourcePath,
                new MaskingContext(this.Includes, this.Excludes)
            ).ConfigureAwait(false);

            var files = infos.OfType<SlimFileInfo>();

            foreach (var file in files)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var targetFileName = fileOps.CombinePath(targetPath, file.FullName.Substring(sourcePath.Length).TrimStart('/', '\\'));
                var targetDirectoryName = PathEx.GetDirectoryName(targetFileName);
                if (this.renameFile != null)
                    targetFileName = fileOps.CombinePath(targetDirectoryName, this.renameFile(PathEx.GetFileName(targetFileName)));

                if (this.VerboseLogging)
                    this.LogDebug($"Copying {file.FullName} to {targetFileName}...");

                try
                {
                    if (!this.Overwrite && await fileOps.FileExistsAsync(targetFileName).ConfigureAwait(false))
                    {
                        this.LogError($"Target file {targetFileName} already exists and overwrite is set to false.");
                        continue;
                    }

                    await fileOps.CreateDirectoryAsync(targetDirectoryName).ConfigureAwait(false);
                    await fileOps.CopyFileAsync(file.FullName, targetFileName, this.Overwrite).ConfigureAwait(false);
                    this.filesCopied++;
                }
                catch (Exception ex)
                {
                    this.LogError($"Cannot copy {file.FullName}: {ex.Message}");
                }
            }

            var dirs = infos.OfType<SlimDirectoryInfo>();
            foreach (var dir in dirs)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var targetDir = PathEx.Combine(targetPath, dir.FullName.Substring(sourcePath.Length).TrimStart('/', '\\'));
                if (this.VerboseLogging)
                    this.LogDebug($"Creating directory {targetDir}...");

                await fileOps.CreateDirectoryAsync(targetDir).ConfigureAwait(false);
                this.directoriesCopied++;
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Copy ",
                    new MaskHilite(config[nameof(this.Includes)], config[nameof(this.Excludes)])
                ),
                new RichDescription(
                    "from ",
                    new DirectoryHilite(config[nameof(this.SourceDirectory)]),
                    " to ",
                    new DirectoryHilite(config[nameof(this.TargetDirectory)])
                )
            );
        }
    }
}
