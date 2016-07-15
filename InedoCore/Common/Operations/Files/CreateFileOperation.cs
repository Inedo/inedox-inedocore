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
#endif

namespace Inedo.Extensions.Operations.Files
{
    [DisplayName("Create File")]
    [Description("Creates a file on a server.")]
    [ScriptAlias("Create-File")]
    [Tag(Tags.Files)]
    [Example(@"
# write the name of the current working directory to my desktop
Create-File(
    Name: C:\Users\atripp\Desktop\workingdir.txt,
    Text: $WorkingDirectory
);
")]
    public sealed class CreateFileOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("File name")]
        [Description("The path of the file to create.")]
        public string FileName { get; set; }
        [ScriptAlias("Text")]
        [Description("The contents of the file. If this value is missing or empty, a 0-byte file will be created.")]
        public string Text { get; set; }
        [ScriptAlias("Overwrite")]
        [Description(CommonDescriptions.Overwrite)]
        public bool Overwrite { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Create ",
                    new Hilite(config[nameof(this.FileName)])
                ),
                new RichDescription(
                    "starting with ",
                    new Hilite(config[nameof(this.Text)])
                )
            );
        }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var path = context.ResolvePath(this.FileName);

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
            if (await fileOps.FileExistsAsync(path).ConfigureAwait(false))
            {
                this.LogDebug($"File {path} already exists.");
                if (!this.Overwrite)
                {
                    this.LogError(this.FileName + " already exists and overwrite is set to false.");
                    return;
                }
            }

            this.LogInformation("Creating file...");
            this.LogDebug($"Creating directories for {path}...");
            await fileOps.CreateDirectoryAsync(PathEx.GetDirectoryName(path)).ConfigureAwait(false);
            this.LogDebug($"Creating {path}...");

            await fileOps.WriteAllTextAsync(path, this.Text ?? "").ConfigureAwait(false);

            this.LogInformation(path + " file created.");
        }
    }
}
