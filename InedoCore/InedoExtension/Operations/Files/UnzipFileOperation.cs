using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.Files
{
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
    [Tag("files")]
    public sealed class UnzipFileOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("File name")]
        public string FileName { get; set; }
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        public string TargetDirectory { get; set; }
        [ScriptAlias("ClearTarget")]
        [DisplayName("Clear target directory")]
        public bool ClearTargetDirectory { get; set; }
        [ScriptAlias("Overwrite")]
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
            await fileOps.ExtractZipFileAsync(zipFilePath, targetDirectory, this.Overwrite ? IO.FileCreationOptions.Overwrite : IO.FileCreationOptions.Default).ConfigureAwait(false);

            this.LogInformation(zipFilePath + " extracted.");
        }
    }
}
