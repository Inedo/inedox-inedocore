using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.CopyFilesAction,BuildMasterExtensions")]
    [DisplayName("Copy Files")]
    [Description("Copies files from one directory to another on the same server.")]
    [Tag(Tags.Files)]
    [CustomEditor(typeof(CopyFilesActionEditor))]
    public sealed class CopyFilesAction : RemoteActionBase
    {
        private int filesCopied;
        private int directoriesCopied;

        [Persistent]
        public string[] IncludeFileMasks { get; set; }
        [Persistent]
        public bool Overwrite { get; set; }
        [Persistent]
        public bool VerboseLogging { get; set; }
        [Persistent]
        public bool Recursive { get; set; }

        private bool HasMasks
        {
            get { return this.IncludeFileMasks != null && this.IncludeFileMasks.Length > 0 && !(this.IncludeFileMasks.Length == 1 && this.IncludeFileMasks[0] == "*"); }
        }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Copy files from ",
                    new DirectoryHilite(this.OverriddenSourceDirectory),
                    " to ",
                    new DirectoryHilite(this.OverriddenTargetDirectory)
                ),
                new RichDescription(
                    this.Recursive ? "and recursively copy subdirectories" : "top level only"
                )
            );
        }

        protected override void Execute()
        {
            var maskText = this.HasMasks ? ("items matching " + string.Join(", ", this.IncludeFileMasks)) : "everything";
            this.LogInformation($"Copying {maskText} from {this.Context.SourceDirectory} to {this.Context.TargetDirectory}...");

            if (this.Context.Agent.TryGetService<IRemoteJobExecuter>() != null)
            {
                this.LogDebug("Agent supports remote commands; performing optimized copy...");
                this.ExecuteRemoteCommand("CopyFiles");
            }
            else
            {
                this.CopyDirectoryRemote(this.Context.SourceDirectory, this.Context.TargetDirectory);
            }
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            this.CopyDirectoryLocal(this.Context.SourceDirectory, this.Context.TargetDirectory);
            this.LogDebug($"Copied {this.filesCopied} files and {this.directoriesCopied} directories.");
            return string.Empty;
        }

        private void CopyDirectoryLocal(string sourcePath, string targetPath)
        {
            var entry = Util.Files.GetDirectoryEntry(
                new GetDirectoryEntryCommand
                {
                    IncludeRootPath = true,
                    Path = sourcePath,
                    Recurse = this.Recursive
                }
            ).Entry;

            IEnumerable<SystemEntryInfo> matches;
            if (this.HasMasks)
                matches = Util.Files.Comparison.GetMatches(sourcePath, entry, this.IncludeFileMasks);
            else
                matches = entry.Files.AsEnumerable<SystemEntryInfo>().Concat(entry.SubDirectories);

            matches = matches.Where(m => m.Path != entry.Path);

            var files = matches.OfType<FileEntryInfo>();
            foreach (var file in files)
            {
                var targetFileName = PathEx.Combine(targetPath, file.Path.Substring(sourcePath.Length).TrimStart('/', '\\'));
                if (this.VerboseLogging)
                    this.LogDebug($"Copying {file.Path} to {targetFileName}...");

                try
                {
                    Inedo.IO.FileEx.Copy(file.Path, targetFileName, this.Overwrite);
                    this.filesCopied++;
                }
                catch (Exception ex)
                {
                    this.LogError($"Cannot copy {file.Path}: {ex.Message}");
                }
            }

            // ToArray is just to make entries subject to GC sooner
            var dirs = matches.OfType<DirectoryEntryInfo>().ToArray();
            foreach (var dir in dirs)
            {
                var targetDir = PathEx.Combine(targetPath, dir.Path.Substring(sourcePath.Length).TrimStart('/', '\\'));
                if (this.VerboseLogging)
                    this.LogDebug($"Creating directory {targetDir}...");

                DirectoryEx.Create(targetDir);
                this.directoriesCopied++;
                this.CopyDirectoryLocal(dir.Path, targetDir);
            }
        }

        private void CopyDirectoryRemote(string sourcePath, string targetPath)
        {
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();

            var rootEntry = fileOps.GetDirectoryEntry(
                new GetDirectoryEntryCommand
                {
                    IncludeRootPath = true,
                    Path = sourcePath,
                    Recurse = this.Recursive
                }
            ).Entry;

            IEnumerable<SystemEntryInfo> matches;
            if (this.HasMasks)
                matches = Util.Files.Comparison.GetMatches(sourcePath, rootEntry, this.IncludeFileMasks);
            else
                matches = rootEntry.FlattenWithFiles().OfType<FileEntryInfo>();

            var filesToCopy = matches
                .OfType<FileEntryInfo>()
                .Select(e => e.Path.Substring(sourcePath.Length).TrimStart('/', '\\'))
                .ToArray();

            if (this.VerboseLogging)
            {
                foreach (var file in filesToCopy)
                    this.LogDebug($"Copying {PathEx.Combine(sourcePath, file)} to {PathEx.Combine(targetPath, file)}...");
            }

            fileOps.FileCopyBatch(sourcePath, filesToCopy, targetPath, filesToCopy, this.Overwrite, true);
            this.LogDebug($"Copied {filesToCopy.Length} files.");
        }
    }
}
