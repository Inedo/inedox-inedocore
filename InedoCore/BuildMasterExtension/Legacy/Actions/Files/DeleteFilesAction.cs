using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;
using Inedo.Agents;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.DeleteFilesAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.Files.DeleteFiles))]
    [DisplayName("Delete Files/Folders")]
    [Description("Deletes files and folders in a directory based on one or more specified file masks.")]
    [Inedo.Web.CustomEditor(typeof(DeleteFilesActionEditor))]
    [RequiresInterface(typeof(IFileOperationsExecuter))]
    [Tag(Tags.Files)]
    public sealed class DeleteFilesAction : AgentBasedActionBase
    {
        [Persistent]
        public string[] FileMasks { get; set; }

        [Persistent]
        public bool LogVerbose { get; set; }

        [Persistent]
        public bool Recursive { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Delete ",
                    new ListHilite((this.FileMasks ?? new string[0]).DefaultIfEmpty("*"))
                ),
                new RichDescription(
                    "from ",
                    new DirectoryHilite(this.OverriddenSourceDirectory)
                )
            );
        }

        protected override void Execute()
        {
            var fileMasks = (this.FileMasks ?? new string[0])
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct()
                .DefaultIfEmpty("*")
                .ToArray();

            this.LogInformation("Deleting: " + string.Join(", ", fileMasks));

            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();

            // Optimization for clearing a folder
            if (fileMasks.Length == 1 && fileMasks[0] == "*" && !this.LogVerbose)
            {
                fileOps.ClearDirectory(this.Context.SourceDirectory);
                this.LogInformation("Directory cleared.");
                return;
            }

            var rootEntry = fileOps.GetDirectoryEntry(
                new GetDirectoryEntryCommand
                {
                    Path = this.Context.SourceDirectory,
                    Recurse = this.Recursive,
                    IncludeRootPath = true
                }
            ).Entry;

            // Get the matches and make sure the root is not included
            // Sort by the number of directory separators in descending order so subdirs are deleted first
            var matches = Util.Files.Comparison.GetMatches(
                this.Context.SourceDirectory,
                rootEntry,
                fileMasks
            )
            .Where(m => m.Path != rootEntry.Path)
            .OrderBy(m => m.Path.Where(c => c == '/' || c == '\\').Count())
            .ToList();
            matches.Reverse();

            var filesToDelete = matches
                .OfType<FileEntryInfo>()
                .Select(e => e.Path)
                .ToArray();

            var directoriesToDelete = matches
                .OfType<DirectoryEntryInfo>()
                .Select(e => e.Path)
                .ToArray();

            if (this.LogVerbose)
            {
                foreach (var fileName in filesToDelete)
                    this.LogDebug("Deleting file: {0}", fileName);
            }

            fileOps.DeleteFiles(filesToDelete);

            if (this.LogVerbose)
            {
                foreach (var dirName in directoriesToDelete)
                    this.LogDebug("Deleting directory: {0}", dirName);
            }

            fileOps.DeleteDirectories(directoriesToDelete);

            this.LogInformation("Deleted {0} files and {1} directories", filesToDelete.Length, directoriesToDelete.Length);
        }
    }
}
