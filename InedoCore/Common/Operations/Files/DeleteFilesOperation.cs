using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
#if BuildMaster
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Documentation;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.Files
{
    [DisplayName("Delete Files")]
    [Description("Deletes files on a server.")]
    [ScriptAlias("Delete-Files")]
    [Note("This operation will delete files one-by-one. To clear large directories, a PowerShell script may be more performant.")]
    [DefaultProperty(nameof(Includes))]
    [Tag(Tags.Files)]
    [Example(@"
# delete all .config files in the working directory except web.config
Delete-Files(
    Include: *.config,
    Exlude: web.config
);
")]
    public sealed class DeleteFilesOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("Include")]
        [Description(CommonDescriptions.MaskingHelp)]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
        [Description(CommonDescriptions.MaskingHelp)]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("Directory")]
        [Description(CommonDescriptions.SourceDirectory)]
        public string SourceDirectory { get; set; }
        [ScriptAlias("Verbose")]
        [DisplayName("Verbose")]
        [Description(CommonDescriptions.VerboseLogging)]
        public bool VerboseLogging { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Delete ",
                    new MaskHilite(config[nameof(this.Includes)], config[nameof(this.Excludes)])
                ),
                new RichDescription(
                    "in ",
                    new DirectoryHilite(config[nameof(this.SourceDirectory)])
                )
            );
        }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var sourceDirectory = context.ResolvePath(this.SourceDirectory);

            this.LogDebug($"Deleting files from {sourceDirectory}...");

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            if (!await fileOps.DirectoryExistsAsync(sourceDirectory).ConfigureAwait(false))
            {
                this.LogInformation(sourceDirectory + " does not exist.");
                return;
            }

            var mask = new MaskingContext(this.Includes, this.Excludes);

            // Optimization for clearing a folder
            if (mask.Includes.FirstOrDefault() == "**" && !mask.Excludes.Any() && !this.VerboseLogging)
            {
                this.LogDebug("Mask indicates that files and directories should be deleted recursively, clearing directory...");
                await fileOps.ClearDirectoryAsync(sourceDirectory).ConfigureAwait(false);
                this.LogInformation("Directory cleared.");
                return;
            }

            // Get the matches and make sure the root is not included
            // Sort by the number of directory separators in descending order so subdirs are deleted first
            var files = (await fileOps.GetFileSystemInfosAsync(sourceDirectory, mask).ConfigureAwait(false))
                .OrderByDescending(m => m.FullName.Where(c => c == '/' || c == '\\').Count())
                .ToList();

            var filesToDelete = files
                .OfType<SlimFileInfo>()
                .Select(e => e.FullName)
                .ToArray();

            var directoriesToDelete = files
                .OfType<SlimDirectoryInfo>()
                .Select(e => e.FullName)
                .ToArray();

            if (this.VerboseLogging)
            {
                foreach (var fileName in filesToDelete)
                    this.LogDebug($"Deleting file: {fileName}");
            }

            await fileOps.DeleteFilesAsync(filesToDelete).ConfigureAwait(false);

            if (this.VerboseLogging)
            {
                foreach (var dirName in directoriesToDelete)
                    this.LogDebug($"Deleting directory: {dirName}");
            }

            await fileOps.DeleteDirectoriesAsync(directoriesToDelete).ConfigureAwait(false);

            this.LogInformation($"Deleted {filesToDelete.Length} files and {directoriesToDelete.Length} directories.");
        }
    }
}
