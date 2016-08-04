using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Agents;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.TransferFilesAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.Files.TransferFiles))]
    [DisplayName("Transfer/Synchronize Files")]
    [Description("Synchronizes the contents of a source directory with a target directory on the same or different server, similar to rsync or robocopy.")]
    [CustomEditor(typeof(TransferFilesActionEditor))]
    [Tag(Tags.Files)]
    public sealed class TransferFilesAction : DualAgentBasedActionBase
    {
        private string[] _IncludeFileMasks = new string[0];

        [Persistent]
        public string SourceDirectory { get; set; }

        [Persistent]
        public string TargetDirectory { get; set; }

        [Persistent]
        public bool DeleteTarget { get; set; }

        [Persistent]
        public string[] IncludeFileMasks
        {
            get { return this._IncludeFileMasks; }
            set { this._IncludeFileMasks = value ?? new string[0]; }
        }

        public override ExtendedRichDescription GetActionDescription()
        {
            if (this.DeleteTarget)
            {
                return new ExtendedRichDescription(
                    new RichDescription("Synchronize Directory Contents"),
                    new RichDescription("of ", new DirectoryHilite(this.TargetDirectory), " with files and directories from ", new DirectoryHilite(this.SourceDirectory))
                );
            }
            else
            {
                return new ExtendedRichDescription(
                    new RichDescription("Transfer Modified Files/Directories"),
                    new RichDescription("of ", new DirectoryHilite(this.SourceDirectory), " to ", new DirectoryHilite(this.TargetDirectory))
                );
            }
        }

        protected override void Execute()
        {
            var destServer = DB.Servers_GetServer(this.Context.TargetServerId).Servers.FirstOrDefault();
            this.LogDebug("Target server ID: {0}", this.Context.TargetServerId);
            if (destServer == null)
            {
                this.LogError("Target server does not exist and may have been deleted.", this.Context.TargetServerId);
                return;
            }

            var sourceAgent = this.Context.SourceAgent.GetService<IFileOperationsExecuter>();
            char srcSeparator = sourceAgent.DirectorySeparator;
            var srcPath = sourceAgent.GetLegacyWorkingDirectory((IGenericBuildMasterContext)this.Context, this.SourceDirectory);

            this.LogDebug("Full source directory: {0}", srcPath);

            this.LogDebug("Loading source file list...");
            var sourceDir = sourceAgent.GetDirectoryEntry(
                new GetDirectoryEntryCommand
                {
                    Path = srcPath,
                    Recurse = true,
                    IncludeRootPath = true
                }
            ).Entry;

            if (destServer.ServerType_Code == Domains.ServerTypeCodes.Server)
            {
                this.LogInformation("Transferring files to {0}...", destServer.Server_Name);
                this.TransferFiles(sourceAgent, srcSeparator, srcPath, sourceDir, this.Context.TargetServerId);
            }
            else
            {
                this.LogDebug("Target server is a server group, transferring to each server in the group...");
                var targetServers = DB.Servers_GetServersInGroup(this.Context.TargetServerId);
                foreach (var server in targetServers)
                {
                    this.LogInformation("Transferring files to {0}...", server.Server_Name);
                    this.TransferFiles(sourceAgent, srcSeparator, srcPath, sourceDir, server.Server_Id);
                }
            }

            this.LogInformation("File transfer completed.");
        }

        private void TransferFiles(IFileOperationsExecuter sourceAgentHelper, char srcSeparator, string srcPath, DirectoryEntryInfo sourceDir, int targetServerId)
        {
            using (var targetAgent = Util.Agents.CreateAgentFromId(targetServerId))
            {
                var fileOps = targetAgent.GetService<IFileOperationsExecuter>();
                char targetSeparator = fileOps.DirectorySeparator;

                var tarPath = fileOps.GetLegacyWorkingDirectory(
                    (IGenericBuildMasterContext)this.Context,
                    this.TargetDirectory
                );

                this.LogDebug("Full target directory: {0}", tarPath);
                fileOps.CreateDirectory(tarPath);

                this.LogDebug("Loading target file list...");
                var targetDir = fileOps.GetDirectoryEntry(
                    new GetDirectoryEntryCommand
                    {
                        Path = tarPath,
                        IncludeRootPath = true,
                        Recurse = true
                    }
                ).Entry;

                this.LogDebug("Performing directory comparison...");
                List<string> filesToCopy = new List<string>(),
                    foldersToCopy = new List<string>(),
                    filesToDelete = new List<string>(),
                    foldersToDelete = new List<string>();
                Util.Files.Comparison.CompareDirectories(
                    sourceDir, targetDir,
                    filesToCopy, foldersToCopy,
                    filesToDelete, foldersToDelete);

                // Make sure target files and folders to delete are canonical paths
                for (int i = 0; i < filesToDelete.Count; i++)
                    filesToDelete[i] = filesToDelete[i].Replace(srcSeparator, targetSeparator);
                for (int i = 0; i < foldersToDelete.Count; i++)
                    foldersToDelete[i] = foldersToDelete[i].Replace(srcSeparator, targetSeparator);

                // Run Filters
                if (this.IncludeFileMasks.Length != 0 &&
                    !(this.IncludeFileMasks.Length == 1 &&
                      string.IsNullOrEmpty(this.IncludeFileMasks[0])))
                {
                    filesToCopy = new List<string>(Util.Files.Comparison
                        .GetMatches(srcPath, filesToCopy.ToArray(), IncludeFileMasks));
                    foldersToCopy = new List<string>(Util.Files.Comparison
                        .GetMatches(srcPath, foldersToCopy.ToArray(), IncludeFileMasks));
                    filesToDelete = new List<string>(Util.Files.Comparison
                        .GetMatches(tarPath, filesToDelete.ToArray(), IncludeFileMasks));
                    foldersToDelete = new List<string>(Util.Files.Comparison
                        .GetMatches(tarPath, foldersToDelete.ToArray(), IncludeFileMasks));
                }

                if (this.DeleteTarget)
                {
                    this.LogInformation("Deleting files and directories that are not present in the source directory...");

                    if (filesToDelete.Count == 0)
                        this.LogDebug("No files to delete in target directory.");
                    else
                        this.LogDebug("Deleting {0} files from target directory...", filesToDelete.Count);

                    foreach (string path in filesToDelete)
                        this.LogDebug("\t" + path.Substring(tarPath.Length));

                    fileOps.DeleteFiles(filesToDelete.ToArray());

                    if (foldersToDelete.Count == 0)
                        this.LogDebug("No directories to delete in target directory.");
                    else
                        this.LogDebug("Deleting {0} directories from target directory...", foldersToDelete.Count);

                    foreach (string path in foldersToDelete)
                        this.LogDebug("\t" + path.Substring(tarPath.Length));

                    fileOps.DeleteDirectories(foldersToDelete.ToArray());
                }
                else
                {
                    this.LogDebug("Files and directories not present in source directory will not be deleted from target directory.");
                }

                this.LogInformation("Creating missing directories in target directory...");
                if (foldersToCopy.Count == 0)
                    this.LogDebug("No directories missing in target directory.");
                else
                    this.LogDebug("Creating {0} directories in target directory...", foldersToCopy.Count);

                foreach (string directoryToCopy in foldersToCopy)
                {
                    string relativeTargetPath = directoryToCopy.Substring(srcPath.Length)
                                                               .Replace(srcSeparator, targetSeparator);

                    if (relativeTargetPath.StartsWith(targetSeparator.ToString()))
                        relativeTargetPath = relativeTargetPath.Substring(1);

                    this.LogDebug("\t" + relativeTargetPath);
                    fileOps.CreateDirectory(fileOps.CombinePath(tarPath, relativeTargetPath));
                }

                this.LogInformation("Copying or transferring modified files from source directory to target directory...");

                if (filesToCopy.Count == 0)
                    this.LogDebug("No files to copy to the target directory.");
                else
                    this.LogDebug("Copying {0} files to the target directory...", filesToCopy.Count);

                // Build list of source and target files to copy.
                var sourceFilesToCopy = new List<string>(filesToCopy.Count);
                var destFilesToCreate = new List<string>(filesToCopy.Count);
                foreach (var fileToCopy in filesToCopy)
                {
                    var relativeSourcePath = fileToCopy.Substring(srcPath.Length)
                                                       .TrimStart(srcSeparator);
                    sourceFilesToCopy.Add(relativeSourcePath);

                    var relativeDestPath = relativeSourcePath.Replace(srcSeparator, targetSeparator);
                    destFilesToCreate.Add(relativeDestPath);

                    // These are copied in a batch, so can't get logged all at once.
                    if (this.Context.SourceServerId == targetServerId)
                        this.LogDebug("Copying all files from: {0}", relativeDestPath);
                }

                if (this.Context.SourceServerId == targetServerId)
                {
                    this.LogDebug("Both source and target servers are the same, performing batch copy of files...");
                    fileOps.FileCopyBatch(srcPath, sourceFilesToCopy.ToArray(), tarPath, destFilesToCreate.ToArray(), true, true);
                }
                else
                {
                    this.LogDebug("Transferring files from source server to target server...");
                    for (int i = 0; i < sourceFilesToCopy.Count; i++)
                    {
                        this.LogDebug("\t" + destFilesToCreate[i]);

                        var fullSourcePath = CombinePath(srcPath, sourceFilesToCopy[i], srcSeparator);
                        var fullTargetPath = CombinePath(tarPath, destFilesToCreate[i], targetSeparator);

                        string targetDirectory = PathEx.GetDirectoryName(fullTargetPath);
                        if (!string.IsNullOrEmpty(targetDirectory))
                        {
                            try
                            {
                                fileOps.CreateDirectory(targetDirectory);
                            }
                            catch (Exception ex)
                            {
                                this.LogDebug("Could not create directory at \"{0}\"; error was: {1}", targetDirectory, ex.Message);
                            }
                        }

                        Util.Files.TransferFile(sourceAgentHelper, fullSourcePath, fileOps, fullTargetPath);

                        var lastModified = sourceAgentHelper.GetFileInfo(fullSourcePath).LastWriteTimeUtc;
                        fileOps.SetLastWriteTime(fullTargetPath, lastModified);
                    }
                }
            }
        }

        /// <summary>
        /// Combines two paths using a specified directory separator.
        /// </summary>
        /// <param name="path1">First path.</param>
        /// <param name="path2">Second path.</param>
        /// <param name="separator">Directory separator.</param>
        /// <returns>Combined path.</returns>
        private static string CombinePath(string path1, string path2, char separator)
        {
            if (string.IsNullOrEmpty(path1)) return path2 ?? string.Empty;
            if (string.IsNullOrEmpty(path2)) return path1 ?? string.Empty;

            path1 = path1 ?? string.Empty;
            path2 = path2 ?? string.Empty;

            if (path1.Length > 1)
                path1 = path1.TrimEnd(separator);

            path2 = path2.TrimStart(separator);

            return path1 + separator + path2;
        }
    }
}
