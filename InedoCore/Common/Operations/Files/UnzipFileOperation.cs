using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
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
    [DisplayName("Extract Zip File")]
    [Description("Extracts a zip file on a server.")]
    [ScriptAlias("Extract-ZipFile")]
    [ScriptNamespace(Namespaces.Files, PreferUnqualified = true)]
    [DefaultProperty(nameof(FileName))]
    [SeeAlso(typeof(CreateZipFileOperation))]
    [Example(@"
# extracts archive.zip in ArchiveContents after deleting the directory contents
Extract-ZipFile(
    Name: archive.zip,
    Directory: E:\Services\ArchiveContents,
    ClearTarget: true
);
")]
    [Tag(Tags.Files)]
    public sealed class UnzipFileOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("File name")]
        [Description("The file path of the zip archive to extract.")]
        public string FileName { get; set; }
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory where the files will be extracted to. The default is the working directory")]
        public string TargetDirectory { get; set; }
        [ScriptAlias("ClearTarget")]
        [DisplayName("Clear target directory")]
        [Description("When true, the target directory will be cleared before extracting the contents of the zip file.")]
        public bool ClearTargetDirectory { get; set; }
        [ScriptAlias("Overwrite")]
        [Description(CommonDescriptions.Overwrite)]
        public bool Overwrite { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Unzip ",
                    new DirectoryHilite(config[nameof(FileName)])
                ),
                new RichDescription(
                    "to ",
                    new DirectoryHilite(config[nameof(TargetDirectory)])
                )
            );
        }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var targetDirectory = context.ResolvePath(this.TargetDirectory);
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var zipFilePath = context.ResolvePath(this.FileName);
            this.LogInformation($"Extracting zip file {zipFilePath}...");

            if (!await fileOps.DirectoryExistsAsync(targetDirectory).ConfigureAwait(false))
            {
                this.LogDebug($"Target directory {targetDirectory} does not exist; creating...");
                await fileOps.CreateDirectoryAsync(targetDirectory).ConfigureAwait(false);
            }

            if (this.ClearTargetDirectory)
            {
                this.LogDebug($"Clearing {targetDirectory}...");
                await fileOps.ClearDirectoryAsync(targetDirectory).ConfigureAwait(false);
            }

            this.LogDebug($"Unzipping {zipFilePath} to {targetDirectory}...");
            await fileOps.ExtractZipFileAsync(zipFilePath, targetDirectory, this.Overwrite).ConfigureAwait(false);

            this.LogInformation(zipFilePath + " extracted.");
        }
    }
}
