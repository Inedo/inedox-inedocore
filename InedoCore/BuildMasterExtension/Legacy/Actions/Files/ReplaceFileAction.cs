using System;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using Inedo.Documentation;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.ReplaceFileAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.Files.ReplaceText))]
    [DisplayName("Search/Replace File Contents")]
    [Description("Searches a text file for a specified string and replaces it.")]
    [CustomEditor(typeof(ReplaceFileActionEditor))]
    [Tag(Tags.Files)]
    public sealed class ReplaceFileAction : RemoteActionBase
    {
        /// <summary>
        /// Gets or sets the serialized array of file name masks.
        /// </summary>
        /// <remarks>
        /// This property is here to retain XML-persisted compatibility with
        /// previous versions of this action that supported only one file name.
        /// </remarks>
        [Persistent]
        private string FileName { get; set; }

        /// <summary>
        /// Gets or sets the set of masks of files to search/replace.
        /// </summary>
        public string[] FileNameMasks
        {
            get { return Persistence.DeserializeToStringArray(this.FileName ?? string.Empty); }
            set { this.FileName = Persistence.SerializeStringArray(value ?? new string[0]); }
        }

        /// <summary>
        /// Gets or sets the text to search for
        /// </summary>
        [Persistent]
        public string SearchText { get; set; }

        /// <summary>
        /// Gets or sets the text to replace
        /// </summary>
        [Persistent]
        public string ReplaceText { get; set; }

        /// <summary>
        /// Indicates whether the searching should be done with regular expressions
        /// </summary>
        [Persistent]
        public bool UseRegex { get; set; }

        /// <summary>
        /// Gets or sets whether the search will be recursive, i.e. impacting all sub directories
        /// </summary>
        [Persistent]
        public bool Recursive { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Replace Text in ",
                    new ListHilite(this.FileNameMasks)
                ),
                new RichDescription(
                    "matching ",
                    new Hilite(this.SearchText),
                    " with ",
                    new Hilite(this.ReplaceText),
                    " in ",
                    new DirectoryHilite(this.OverriddenSourceDirectory)
                )
            );
        }

        protected override void Execute()
        {
            if (string.IsNullOrEmpty(this.SearchText))
                throw new InvalidOperationException("SearchText is blank");

            this.ExecuteRemoteCommand(string.Empty);
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            Exception[] exceptionsToIgnore;

            var rootDir = Util.Files.GetDirectoryEntry(
                this.Context.SourceDirectory,
                out exceptionsToIgnore,
                this.Recursive);

            var masks = this.FileNameMasks;

            // Special handling for one item with no directory or masking
            // characters: prepend */
            if (this.Recursive && masks.Length == 1 && !string.IsNullOrEmpty(masks[0]) && masks[0].IndexOfAny(new[] { '*', '!', '/', '\\' }) < 0)
            {
                var fileName = masks[0];
                masks = new[] { fileName, "*" + Path.DirectorySeparatorChar.ToString() + fileName };
            }

            var itemsToReplace = Util.Files.Comparison.GetMatches(
                this.Context.SourceDirectory,
                rootDir,
                masks);

            if (itemsToReplace.Length == 0)
                LogInformation("No matching files were found.");

            foreach (var filePath in itemsToReplace)
            {
                // Skip directory entries.
                if (filePath is DirectoryEntryInfo)
                    continue;

                var fileText = File.ReadAllText(filePath.Path);

                int matchCount = UseRegex
                        ? Regex.Matches(fileText, SearchText).Count
                        : (fileText.Length - fileText.Replace(SearchText, string.Empty).Length) / SearchText.Length;

                if (matchCount > 0)
                    LogInformation(string.Format("{0}: {1} replacement(s) found.", filePath.Path, matchCount));

                // Clear read-only flag if necessary.
                var flags = File.GetAttributes(filePath.Path);
                if ((flags & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(filePath.Path, flags | FileAttributes.ReadOnly);

                File.WriteAllText(
                    filePath.Path,
                    UseRegex
                        ? Regex.Replace(fileText, SearchText, ReplaceText)
                        : fileText.Replace(SearchText, ReplaceText)
                );
            }

            return string.Empty;
        }
    }
}
