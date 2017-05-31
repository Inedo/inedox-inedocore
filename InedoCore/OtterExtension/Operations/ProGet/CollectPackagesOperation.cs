using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.UniversalPackages;
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet
{
    [ScriptAlias("Collect-InstalledPackages")]
    [DisplayName("Universal packages")]
    [Description("Collect list of installed universal packages")]
    [ScriptNamespace(Namespaces.ProGet)]
    public sealed class CollectPackagesOperation : CollectOperation
    {
        public override async Task<PersistedConfiguration> CollectAsync(IOperationExecutionContext context)
        {
            using (var registry = await PackageRegistry.GetRegistryAsync(context.Agent, false).ConfigureAwait(false))
            {
                await registry.LockAsync(context.CancellationToken).ConfigureAwait(false);

                foreach (var p in await registry.GetInstalledPackagesAsync().ConfigureAwait(false))
                {
#warning this does not handle uninstalled packages
                    DB.ServerPackages_CreateOrUpdatePackage(
                        Server_Id: context.ServerId,
                        PackageType_Name: "ProGet",
                        Package_Name: p.Name,
                        Package_Version: p.Version,
                        CollectedOn_Execution_Id: context.ExecutionId,
                        Url_Text: p.FeedUrl
                    );
                }

                // doesn't need to be in a finally because dispose will unlock if necessary, but prefer doing it asynchronously
                await registry.UnlockAsync().ConfigureAwait(false);
            }

            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config) => new ExtendedRichDescription(new RichDescription("Collect installed universal packages"));
    }
}
