using System.ComponentModel;
using System.IO;
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
    [ScriptNamespace(Namespaces.Files, PreferUnqualified = true)]
    [Tag(Tags.Files)]
    [DefaultProperty(nameof(FileName))]
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
        public string Text { get; set; }
        [ScriptAlias("Overwrite")]
        public bool Overwrite { get; set; }

        [Category("Linux")]
        [ScriptAlias("FileMode")]
        [DisplayName("File mode")]
        [Description("The octal file mode for the file. This value is ignored on Windows.")]
        [DefaultValue("644")]
        public string PosixFileMode { get; set; }

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
            var linuxFileOps = fileOps as ILinuxFileOperationsExecuter;
            if (linuxFileOps != null)
            {
                int? mode = AH.ParseInt(AH.CoalesceString(this.PosixFileMode ?? "644"));
                if (mode == null)
                {
                    this.LogError("Invalid file mode specified.");
                    return;
                }

                using (var stream = await linuxFileOps.OpenFileAsync(path, FileMode.Create, FileAccess.Write, Extensions.PosixFileMode.FromDecimal(mode.Value).OctalValue))
                using (var writer = new StreamWriter(stream, InedoLib.UTF8Encoding) { NewLine = linuxFileOps.NewLine })
                {
                    await writer.WriteAsync(this.Text ?? string.Empty);
                }
            }
            else
            {
                await fileOps.WriteAllTextAsync(path, this.Text ?? string.Empty);
            }

            this.LogInformation(path + " file created.");
        }
    }
}
