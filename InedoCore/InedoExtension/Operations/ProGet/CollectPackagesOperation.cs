using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.UniversalPackages;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using System.Linq;
using Inedo.Extensions.Configurations.ProGet;

namespace Inedo.Extensions.Operations.ProGet
{
    [ScriptAlias("Collect-InstalledPackages")]
    [DisplayName("Universal packages")]
    [Description("Collect list of installed universal packages")]
    [ScriptNamespace(Namespaces.ProGet)]
    public sealed class CollectPackagesOperation : Extensibility.Operations.CollectPackagesOperation
    {
        public override string PackageType => "UPack";

        protected async override Task<IEnumerable<PackageConfiguration>> CollectPackagesAsync(IOperationCollectionContext context)
        {
            IList<RegisteredPackage> packages;
            this.LogDebug("Connecting to machine package registry...");
            using (var registry = await RemotePackageRegistry.GetRegistryAsync(context.Agent, false).ConfigureAwait(false))
            {
                this.LogDebug("Acquiring package registry lock...");
                await registry.LockAsync(context.CancellationToken).ConfigureAwait(false);
                this.LogDebug($"Package registry lock acquired (token={registry.LockToken}).");

                this.LogInformation("Retrieving list of packages...");
                packages = await registry.GetInstalledPackagesAsync().ConfigureAwait(false);
                this.LogInformation("Packages installed: " + packages.Count);

                // doesn't need to be in a finally because dispose will unlock if necessary, but prefer doing it asynchronously
                await registry.UnlockAsync().ConfigureAwait(false);
            }
            this.LogDebug("Recording installed packages...");
            return packages.Select(p => 
                new UniveralPackagesConfiguration
                {
                    PackageName = string.IsNullOrWhiteSpace(p.Group) ? p.Name : (p.Group + "/" + p.Name),
                    PackageVersion = p.Version,
                    ViewPackageUrl = p.FeedUrl,
                    Path = p.InstallPath,
                    Cached = false,
                    Date = p.InstallationDate,
                    Reason = p.InstallationReason,
                    Tool = p.InstalledUsing,
                    User = p.InstalledBy
                });
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config) => new ExtendedRichDescription(new RichDescription("Collect installed universal packages"));
    }
}