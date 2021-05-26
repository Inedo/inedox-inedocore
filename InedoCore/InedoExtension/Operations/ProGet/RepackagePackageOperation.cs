using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [Tag("proget")]
    [ScriptAlias("Repack-Package")]
    [DisplayName("Repackage Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Description("Uses ProGet's Repackaging feature to Creates a new package with an altered version number to a ProGet feed and adds a repackaging entry to its metadata for auditing.")]
    public sealed class RepackagePackageOperation : RemoteExecuteOperation, IFeedPackageConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public string PackageSourceName { get; set; }

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }

        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("\"attached\" (BuildMaster only; otherwise required)")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Required]
        [ScriptAlias("NewVersion")]
        [DisplayName("New version")]
        public string NewVersion { get; set; }

        [ScriptAlias("Reason")]
        [DisplayName("Reason")]
        [PlaceholderText("Unspecified")]
        public string Reason { get; set; }


        [Category("Connection/Identity")]
        [ScriptAlias("Feed")]
        [DisplayName("Feed name")]
        [PlaceholderText("Use Feed from package source")]
        [SuggestableValue(typeof(FeedNameSuggestionProvider))]
        public string FeedName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from package source")]
        public string FeedUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access this feed.")]
        [PlaceholderText("Use user name from package source")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [PlaceholderText("Use password from package source")]
        [Description("The password of a user in ProGet that can access this feed.")]
        public string Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiKey")]
        [DisplayName("ProGet API Key")]
        [PlaceholderText("Use API Key from package source")]
        [Description("An API Key that can access this feed.")]
        public string ApiKey { get; set; }

        [Undisclosed]
        [ScriptAlias("Group")]
        public string PackageGroup { get; set; }

        [NonSerialized]
        private IPackageManager packageManager;
        [NonSerialized]
        private string originalPackageSourceName;

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            this.originalPackageSourceName = this.PackageSourceName;
            
            if (!string.IsNullOrEmpty(this.PackageGroup))
                this.PackageName = this.PackageGroup + "/" + this.PackageName;

            await this.ResolveAttachedPackageAsync(context);
            this.PrepareCredentialPropertiesForRemote(context);
            this.packageManager = await context.TryGetServiceAsync<IPackageManager>();
        }
        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            this.LogInformation($"Repackaging {this.PackageName} {this.PackageVersion} to {this.NewVersion} on {this.PackageSourceName} ({this.FeedName} feed)...");

            if (string.Equals(this.PackageVersion, this.NewVersion, StringComparison.OrdinalIgnoreCase))
            {
                this.LogWarning("\"Version\" and \"NewVersion\" are the same; nothing to do.");
                return null;
            }

            var client = new ProGetFeedClient(this.FeedUrl, log: this, cancellationToken: context.CancellationToken);
            await client.RepackageAsync(this, this.NewVersion, this.Reason);
            return true;
        }
        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            await base.AfterRemoteExecuteAsync(result);

            if (this.packageManager != null && result != null && !string.IsNullOrWhiteSpace(this.originalPackageSourceName))
            {
                var packageType = AttachedPackageType.Universal;

                foreach (var p in await this.packageManager.GetBuildPackagesAsync(default))
                {
                    if (p.Active && string.Equals(p.Name, this.PackageName, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Version, this.PackageVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        packageType = p.PackageType;
                        await this.packageManager.DeactivatePackageAsync(p.Name, p.Version, p.PackageSource);
                    }
                }

                await this.packageManager.AttachPackageToBuildAsync(
                    new AttachedPackage(packageType, this.PackageName, this.NewVersion, null, AH.NullIf(this.originalPackageSourceName, string.Empty)),
                    default
                );
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Repackage ",
                    new Hilite(config[nameof(PackageName)]),
                    " ",
                    new Hilite(config[nameof(PackageVersion)]),
                    " to ",
                    new Hilite(config[nameof(NewVersion)])
                ),
                new RichDescription(
                    "on ",
                    new Hilite(config[nameof(PackageSourceName)])
                )
            );
        }
    }
}
