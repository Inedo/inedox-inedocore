using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.Files
{
    [DisplayName("Rename File")]
    [Description("Renames a file on a server.")]
    [ScriptAlias("Rename-File")]
    [Note("To rename multiple files at once, running a PowerShell script is recommended.")]
    [ScriptNamespace(Namespaces.Files, PreferUnqualified = true)]
    [Tag("files")]
    [Example(@"
# renames logs.txt to include the environment name in context
Rename-File (
    From: logs.txt,
    To: logs.$EnvironmentName.txt,
    Overwrite: true
);
")]
    public sealed class RenameFileOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("From")]
        [DisplayName("Source file")]
        public string SourceFileName { get; set; }
        [Required]
        [ScriptAlias("To")]
        [DisplayName("Target file")]
        public string TargetFileName { get; set; }
        [ScriptAlias("Overwrite")]
        [DisplayName("Overwrite")]
        public bool Overwrite { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Rename ",
                    new Hilite(config[nameof(this.SourceFileName)]),
                    " to ",
                    new Hilite(config[nameof(this.TargetFileName)])
                )
            );
        }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var sourcePath = context.ResolvePath(this.SourceFileName);
            var targetPath = context.ResolvePath(this.TargetFileName);

            this.LogInformation($"Renaming {sourcePath} to {targetPath}...");

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            this.LogDebug($"Verifying source file {sourcePath} exists...");
            if (!await fileOps.FileExistsAsync(sourcePath).ConfigureAwait(false))
            {
                this.LogError("Source path does not exist.");
                return;
            }

            if (sourcePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
            {
                this.LogWarning("The source and target file names are the same.");
                return;
            }

            if (!this.Overwrite && await fileOps.FileExistsAsync(targetPath).ConfigureAwait(false))
            {
                this.LogError(this.TargetFileName + " already exists and overwrite is set to false.");
                return;
            }

            await fileOps.MoveFileAsync(sourcePath, targetPath, this.Overwrite).ConfigureAwait(false);

            this.LogInformation("File renamed.");
        }
    }
}
