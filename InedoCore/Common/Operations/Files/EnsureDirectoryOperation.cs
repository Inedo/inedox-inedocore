using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.Configurations.Files;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.Files
{
    [DisplayName("Ensure Directory")]
    [Description("Ensures the existence of a directory on a server.")]
    [ScriptAlias("Ensure-Directory")]
    [ScriptNamespace(Namespaces.Files, PreferUnqualified = true)]
    [Tag("files")]
    [Example(@"
# ensures the Logs directory for the website exists and that it's writable
Ensure-Directory(
    Name: E:\Website\Logs,
    ReadOnly: false
);
")]
    public sealed class EnsureDirectoryOperation : EnsureOperation<DirectoryConfiguration>
    {
#if Otter
        public override async Task<PersistedConfiguration> CollectAsync(IOperationExecutionContext context)
        {
            var path = this.Template.Name;

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            this.LogDebug($"Looking for {path}...");
            if (!await fileOps.DirectoryExistsAsync(path).ConfigureAwait(false))
            {
                this.LogDebug("Directory does not exist.");
                return new DirectoryConfiguration
                {
                    Name = path,
                    Exists = false
                };
            }

            this.LogDebug("Directory exists, loading from disk...");

            var config = new DirectoryConfiguration { Name = this.Template.Name };

            var dir = await fileOps.GetDirectoryInfoAsync(path).ConfigureAwait(false);

            this.LogDebug("Directory configuration loaded.");
            return config;
        }
#endif

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var path = this.Template.Name;

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            this.LogDebug($"Looking for {path}...");
            bool directoryExists = await fileOps.DirectoryExistsAsync(path).ConfigureAwait(false);

            if (!this.Template.Exists)
            {
                if (directoryExists)
                {
                    this.LogDebug("Directory exists, removing...");
                    await fileOps.DeleteDirectoryAsync(path).ConfigureAwait(false);
                    this.LogInformation($"Deleted directory {path}.");
                }
                else
                {
                    this.LogDebug("Directory does not exist.");
                }

                return;
            }

            if (!directoryExists)
            {
                this.LogDebug("Directory does not exist, creating...");
                await fileOps.CreateDirectoryAsync(path).ConfigureAwait(false);
            }

            this.LogInformation($"Directory {path} configured.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var desc = new ExtendedRichDescription(
                new RichDescription(
                    "Ensure directory ",
                    new DirectoryHilite(config[nameof(DirectoryConfiguration.Name)])
                ),
                new RichDescription()
            );

            if (string.Equals(config[nameof(DirectoryConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase))
            {
                desc.LongDescription.AppendContent("does not exist");
                return desc;
            }

            return desc;
        }
    }
}
