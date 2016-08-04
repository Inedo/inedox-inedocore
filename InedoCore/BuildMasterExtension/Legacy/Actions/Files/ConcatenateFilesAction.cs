using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.ConcatenateFilesAction,BuildMasterExtensions")]
    [DisplayName("Concatenate Files")]
    [Description("Concatenates a collection of text files on disk using an optional separator.")]
    [CustomEditor(typeof(ConcatenateFilesActionEditor))]
    [Tag(Tags.Files)]
    public sealed class ConcatenateFilesAction : RemoteActionBase
    {
        private string[] fileMasks = new string[0];
        private string contentSeparationText = string.Empty;

        [Persistent]
        public string[] FileMasks
        {
            get { return this.fileMasks; }
            set
            {
                if (value == null) 
                    this.fileMasks = new string[] { "*" };
                else 
                    fileMasks = value;
            }
        }

        [Persistent]
        public bool Recursive { get; set; }

        [Persistent]
        public string OutputFile { get; set; }

        [Persistent]
        public string OutputFileEncoding { get; set; }

        [Persistent]
        public string ContentSeparationText 
        {
            get { return this.ForceLinuxNewlines ? this.contentSeparationText.Replace("\r\n", "\n") : this.contentSeparationText; } 
            set { this.contentSeparationText = value; } 
        }

        [Persistent]
        public bool ForceLinuxNewlines { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Concatenate ",
                    new ListHilite(this.fileMasks),
                    " into ",
                    new Hilite(this.OutputFile)
                ),
                new RichDescription(
                    "from ",
                    new DirectoryHilite(this.OverriddenSourceDirectory),
                    " to ",
                    new DirectoryHilite(this.OverriddenTargetDirectory)
                )
            );
        }

        protected override void Execute()
        {
            if (string.IsNullOrEmpty(this.OutputFile))
            {
                this.LogError("An output file name was not specified.");
                return;
            }

            if (this.Context.Agent.TryGetService<IRemoteJobExecuter>() != null)
                this.ExecuteRemoteCommand(null);
            else
                this.ConcatenateFiles();
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            this.ConcatenateFiles();
            return string.Empty;
        }

        private void ConcatenateFiles()
        {
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();

            var results = fileOps.GetDirectoryEntry(
                new GetDirectoryEntryCommand
                {
                    Path = this.Context.SourceDirectory,
                    Recurse = this.Recursive,
                    IncludeRootPath = true
                }
            );

            var rootDir = results.Entry;
            var fileEntryMatches = Util.Files.Comparison.GetMatches("", rootDir, this.FileMasks).OfType<FileEntryInfo>().ToList();
            if (fileEntryMatches.Count == 0)
            {
                this.LogWarning("No files to concatenate.");
                return;
            }

            var encoding = this.GetOrGuessEncoding();

            var outputFileName = fileOps.CombinePath(this.Context.TargetDirectory, this.OutputFile);

            this.LogInformation("Concatenating {0} files into {1}...", fileEntryMatches.Count, outputFileName);
            this.LogDebug("Output file encoding will be: " + encoding.EncodingName);

            using (var outputStream = fileOps.OpenFile(outputFileName, FileMode.Create, FileAccess.Write))
            using (var outputWriter = new StreamWriter(outputStream, encoding))
            {
                foreach (var fileEntryMatch in fileEntryMatches)
                {
                    using (var inputStream = fileOps.OpenFile(fileEntryMatch.Path, FileMode.Open, FileAccess.Read))
                    using (var inputReader = new StreamReader(inputStream))
                    {
                        Copy(inputReader, outputWriter);
                        outputWriter.Write(this.ContentSeparationText);
                    }

                    this.ThrowIfCanceledOrTimeoutExpired();
                }
            }

            this.LogInformation("File created: " + outputFileName);
        }

        private Encoding GetOrGuessEncoding()
        {
            if (!string.IsNullOrEmpty(this.OutputFileEncoding))
            {
                if (this.OutputFileEncoding.Equals("ansi", StringComparison.OrdinalIgnoreCase))
                    return Encoding.Default;
                else if (string.Equals(this.OutputFileEncoding, "utf8", StringComparison.OrdinalIgnoreCase))
                    return new UTF8Encoding(false);
                else
                    return Encoding.GetEncoding(this.OutputFileEncoding);
            }
            else
            {
                return new UTF8Encoding(false);
            }
        }

        private static void Copy(TextReader reader, TextWriter writer)
        {
            var buffer = new char[4096];
            int length = reader.Read(buffer, 0, buffer.Length);
            while (length > 0)
            {
                writer.Write(buffer, 0, length);
                length = reader.Read(buffer, 0, buffer.Length);
            }
        }
    }
}
