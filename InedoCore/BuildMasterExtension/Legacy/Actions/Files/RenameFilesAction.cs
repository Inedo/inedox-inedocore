using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using Inedo.Documentation;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.RenameFilesAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.Files.RenameFiles))]
    [DisplayName("Rename Files")]
    [Description("Renames one or more files.")]
    [CustomEditor(typeof(RenameFilesActionEditor))]
    [Tag(Tags.Files)]
    public sealed class RenameFilesAction : RemoteActionBase
    {
        /// <summary>
        /// Gets or sets the mask used to identify files to rename.
        /// </summary>
        [Persistent]
        public string SourceMask { get; set; }

        /// <summary>
        /// Gets or sets the mask applied to a source file to generate the new name.
        /// </summary>
        [Persistent]
        public string TargetMask { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether every file rename should be logged.
        /// </summary>
        [Persistent]
        public bool LogVerbose { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an existing file should be renamed
        /// </summary>
        [Persistent]
        public bool OverwriteExisting { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription("Rename ", new Hilite(this.SourceMask), " to ", new Hilite(this.TargetMask)),
                new RichDescription("in ", new DirectoryHilite(this.OverriddenSourceDirectory))
            );
        }

        protected override void Execute()
        {
            if (string.IsNullOrEmpty(this.SourceMask) || string.IsNullOrEmpty(this.TargetMask))
            {
                this.LogInformation("SourceMask or TargetMask is empty, and thus there is nothing to rename.");
                return;
            }

            this.LogInformation(string.Format("Renaming \"{0}\" to \"{1}\"...", this.SourceMask, this.TargetMask));
            this.ExecuteRemoteCommand("rename");
            this.LogDebug("Rename complete");
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            var directory = new DirectoryInfo(this.Context.SourceDirectory);
            var sourceFiles = directory.GetFiles(this.SourceMask, SearchOption.TopDirectoryOnly);

            if (sourceFiles.Length > 0)
            {
                foreach (var file in sourceFiles)
                {
                    var oldName = file.Name;
                    string newName;

                    try
                    {
                        newName = RenameFile(file, this.TargetMask, this.OverwriteExisting);
                        if (this.LogVerbose)
                            this.LogDebug(string.Format("File '{0}' renamed to '{1}'", oldName, newName));
                    }
                    catch (IOException ex)
                    {
                        this.LogError("Error moving {0}: {1}", oldName, ex.Message);
                    }
                }
            }
            else
            {
                this.LogWarning("No files found to rename.");
            }

            return string.Empty;
        }

        /// <summary>
        /// Renames a file using a mask.
        /// </summary>
        /// <param name="file">File to rename.</param>
        /// <param name="mask">Wildcard mask used to generate the new name.</param>
        /// <param name="overwrite">Whether or not the destination should be overwritten</param>
        /// <returns>New name of the file.</returns>
        private static string RenameFile(FileInfo file, string mask, bool overwrite)
        {
            var newName = ApplyMask(file.Name, mask);
            
            // Do nothing if new name is same as old.
            if (newName == file.Name)
                return file.Name;

            if (File.Exists(Path.Combine(file.DirectoryName, newName)))
            {
                if (overwrite)
                    File.Delete(Path.Combine(file.DirectoryName, newName));
                else
                    throw new IOException("Rename would overwrite an existing file.");
            }

            file.MoveTo(Path.Combine(file.DirectoryName, newName));

            return newName;
        }

        /// <summary>
        /// Transforms a source file name using a wildcard mask.
        /// </summary>
        /// <param name="source">Original file name to transform.</param>
        /// <param name="mask">Wildcard mask to apply to the source file name.</param>
        /// <returns>Transformed file name.</returns>
        /// <remarks>
        /// Given an input file name, this method will produce an output modified by a mask.
        /// For example, a mask of *.doc applied to a source file name of example.txt
        /// will yield example.doc.
        /// The * wildcard will retain 0 or more characters from the source name,
        /// and stop replacing when the next character is encountered in the source.
        /// </remarks>
        private static string ApplyMask(string source, string mask)
        {
            var buffer = new StringBuilder(source);
            var maskReader = new StringReader(mask);

            int i = 0;
            bool inCapture = false;

            int maskValue;
            while ((maskValue = maskReader.Read()) != -1)
            {
                if (maskValue == '?')
                {
                    i++;
                    continue;
                }

                if (maskValue == '*')
                {
                    inCapture = true;
                    continue;
                }

                if (inCapture)
                {
                    while (i < buffer.Length && buffer[i] != maskValue)
                    {
                        i++;
                    }

                    i++;
                    inCapture = false;
                }
                else
                {
                    if (i < buffer.Length)
                        buffer[i] = (char)maskValue;
                    else
                        buffer.Append((char)maskValue);

                    i++;
                }
            }

            return buffer.ToString(0, Math.Min(i, buffer.Length));
        }
    }
}
