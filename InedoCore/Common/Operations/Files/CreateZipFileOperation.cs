using System.ComponentModel;
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
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Configurations;
using Inedo.Hedgehog.Extensibility.Credentials;
using Inedo.Hedgehog.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.Files
{
    [DisplayName("Create Zip File")]
    [Description("Creates a zip file on a server.")]
    [ScriptAlias("Create-ZipFile")]
    [SeeAlso(typeof(UnzipFileOperation))]
    [ScriptNamespace(Namespaces.Files, PreferUnqualified = true)]
    [Tag(Tags.Files)]
    [Example(@"
# zip all log files and place them in the backup directory
Create-ZipFile(
    Name: E:\backup\logs.$Date(""yyyyMMdd-hhmmss"").zip,
    Directory: E:\site\logs
);
")]
    public sealed class CreateZipFileOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Zip file name")]
        public string FileName { get; set; }
        [Required]
        [ScriptAlias("Directory")]
        [DisplayName("Source directory")]
        public string DirectoryToZip { get; set; }
        [ScriptAlias("Overwrite")]
        public bool Overwrite { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var zipFilePath = context.ResolvePath(this.FileName);
            var sourceDirectory = context.ResolvePath(this.DirectoryToZip);

            this.LogDebug($"Zipping {sourceDirectory} to {zipFilePath}...");

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var targetDirectory = PathEx.GetDirectoryName(zipFilePath);
            this.LogDebug($"Ensuring that {targetDirectory} exists...");
            await fileOps.CreateDirectoryAsync(targetDirectory).ConfigureAwait(false);

            this.LogInformation("Creating zip file...");
            if (this.Overwrite)
            {
                this.LogDebug($"Deleting {zipFilePath} if it already exists...");
                await fileOps.DeleteFileAsync(zipFilePath).ConfigureAwait(false);
            }
            else if (await fileOps.FileExistsAsync(zipFilePath).ConfigureAwait(false))
            {
                this.LogDebug(zipFilePath + " aready exists.");
                this.LogError(zipFilePath + " already exists and overwrite is set to false.");
            }

            await fileOps.CreateZipFileAsync(sourceDirectory, zipFilePath).ConfigureAwait(false);

            this.LogInformation(zipFilePath + " file created.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Zip ",
                    new DirectoryHilite(config[nameof(DirectoryToZip)])
                ),
                new RichDescription(
                    "into ",
                    new DirectoryHilite(config[nameof(FileName)])
                )
            );
        }
    }
}
