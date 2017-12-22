#if Otter || Hedgehog
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.UniversalPackages;
#if Otter
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;
using CollectContext = Inedo.Otter.Extensibility.Operations.IOperationExecutionContext;
#else
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using CollectContext = Inedo.Extensibility.Operations.IOperationCollectionContext;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    [ScriptAlias("Collect-InstalledPackages")]
    [DisplayName("Universal packages")]
    [Description("Collect list of installed universal packages")]
    [ScriptNamespace(Namespaces.ProGet)]
    public sealed class CollectPackagesOperation : CollectOperation
    {
        public override async Task<PersistedConfiguration> CollectAsync(CollectContext context)
        {
            IList<RegisteredPackage> packages;
            this.LogDebug("Connecting to machine package registry...");
            using (var registry = await PackageRegistry.GetRegistryAsync(context.Agent, false).ConfigureAwait(false))
            {
                this.LogDebug("Acquiring package registry lock...");
                await registry.LockAsync(context.CancellationToken).ConfigureAwait(false);
                this.LogDebug($"Package registry lock acquired (token={registry.LockToken}).");

                this.LogInformation("Retreiving list of packages...");
                packages = await registry.GetInstalledPackagesAsync().ConfigureAwait(false);
                this.LogInformation("Packages installed: " + packages.Count);

                // doesn't need to be in a finally because dispose will unlock if necessary, but prefer doing it asynchronously
                await registry.UnlockAsync().ConfigureAwait(false);
            }

            this.LogDebug("Recording installed packages...");

#if Otter
            using (var db = new DB.Context())
            {
                db.BeginTransaction();

                await db.ServerPackages_DeletePackagesAsync(
                    Server_Id: context.ServerId,
                    PackageType_Name: "ProGet"
                ).ConfigureAwait(false);

                foreach (var p in packages)
                {
                    await db.ServerPackages_CreateOrUpdatePackageAsync(
                        Server_Id: context.ServerId,
                        PackageType_Name: "ProGet",
                        Package_Name: p.Name,
                        Package_Version: p.Version,
                        CollectedOn_Execution_Id: context.ExecutionId,
                        Url_Text: p.FeedUrl,
                        CollectedFor_ServerRole_Id: context.ServerRoleId
                    ).ConfigureAwait(false);
                }

                db.CommitTransaction();
            }
#else
            using (var collect = context.GetServerCollectionContext())
            {
                await collect.ClearAllPackagesAsync("UPack");

                foreach (var p in packages)
                {
                    await collect.CreateOrUpdateUniversalPackageAsync(
                        "UPack",
                        string.IsNullOrWhiteSpace(p.Group) ? p.Name : (p.Group + "/" + p.Name),
                        p.Version,
                        p.FeedUrl,
                        new CollectedUniversalPackageData
                        {
                            Path = p.InstallPath,
                            Cached = false,
                            Date = p.InstallationDate,
                            Reason = p.InstallationReason,
                            Tool = p.InstalledUsing,
                            User = p.InstalledBy
                        }
                    );
                }
            }
#endif

            this.LogInformation("Package collection complete.");
            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config) => new ExtendedRichDescription(new RichDescription("Collect installed universal packages"));
    }
}
#endif