using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [Tag("proget")]
    [ScriptAlias("Promote-Package")]
    [DisplayName("Promote Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Description("Promotes a package from one feed to another in a ProGet instance.")]
    public sealed class PromotePackageOperation : RemoteExecuteOperation, IFeedPackageConfiguration
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

        [Required]
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("latest")]
        [DefaultValue("latest")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [ScriptAlias("ToFeed")]
        [DisplayName("Promote to Feed")]
        [PlaceholderText("required if not set in Connection/Identity")]
        [SuggestableValue(typeof(FeedNameSuggestionProvider))]
        public string TargetFeedName { get; set; }

        [ScriptAlias("Reason")]
        [DisplayName("Reason")]
        [PlaceholderText("Unspecified")]
        public string Reason { get; set; }


        [Category("Connection/Identity")]
        [DisplayName("To source")]
        [ScriptAlias("To")]
        [PlaceholderText("Same as From package source")]
        public string TargetPackageSourceName { get; set; }

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
        [NonSerialized]
        private string originalTargetPackageSourceName;

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            this.originalPackageSourceName = this.PackageSourceName;
            this.originalTargetPackageSourceName = this.TargetPackageSourceName;

            if (!string.IsNullOrEmpty(this.PackageGroup))
                this.PackageName = this.PackageGroup + "/" + this.PackageName;
            
            if (!string.IsNullOrEmpty(this.TargetPackageSourceName))
            {
                string targetApiEndpointUrl;
                var targetPackageSource = SecureResource.TryCreate(this.TargetPackageSourceName, context as ICredentialResolutionContext ?? CredentialResolutionContext.None);
                if (targetPackageSource is UniversalPackageSource utargetPackageSource)
                    targetApiEndpointUrl = utargetPackageSource.ApiEndpointUrl;
                else if (targetPackageSource is NuGetPackageSource ntargetPackageSource)
                    targetApiEndpointUrl = ntargetPackageSource.ApiEndpointUrl;
                else
                    throw new ExecutionFailureException($"No {nameof(UniversalPackageSource)} or {nameof(NuGetPackageSource)} with the name {this.TargetPackageSourceName} was found.");

                var client = this.TryCreateProGetFeedClient(context);
                var targetClient = new ProGetFeedClient(targetApiEndpointUrl);
                if (targetClient.ProGetBaseUrl != client?.ProGetBaseUrl || targetClient.FeedType != client?.FeedType)
                    throw new ExecutionFailureException($"Target Package Source does have the same feed type and base url as From package source.");

                this.TargetFeedName = targetClient.FeedName;
                this.TargetPackageSourceName = null;
            }

            await this.ResolveAttachedPackageAsync(context);
            this.PrepareCredentialPropertiesForRemote(context);
            this.packageManager = await context.TryGetServiceAsync<IPackageManager>();
        }
        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            this.LogInformation($"Promoting {this.PackageName} {this.PackageVersion} to {this.TargetFeedName}...");

            if (string.Equals(this.TargetFeedName, this.FeedName, StringComparison.OrdinalIgnoreCase))
            {
                this.LogWarning("Target feed and source feed are the same; nothing to do.");
                return null;
            }

            var client = this.TryCreateProGetFeedClient(log: this, token: context.CancellationToken);
            await client.PromoteAsync(this, this.TargetFeedName, this.Reason);
            return true;
        }
        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            await base.AfterRemoteExecuteAsync(result);

            if (this.packageManager != null && result != null && !string.IsNullOrWhiteSpace(this.originalPackageSourceName))
            {
                AttachedPackage package = null;

                foreach (var p in await this.packageManager.GetBuildPackagesAsync(default))
                {
                    if (p.Active && string.Equals(p.Name, this.PackageName, StringComparison.OrdinalIgnoreCase) 
                        && string.Equals(p.Version, this.PackageVersion, StringComparison.OrdinalIgnoreCase) 
                        && string.Equals(p.PackageSource, this.originalPackageSourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        package = p;
                        await this.packageManager.DeactivatePackageAsync(p.Name, p.Version, p.PackageSource);
                    }
                }

                if (package != null)
                {
                    await this.packageManager.AttachPackageToBuildAsync(
                        new AttachedPackage(package.PackageType, package.Name, package.Version, package.Hash, AH.CoalesceString(this.originalTargetPackageSourceName, this.originalPackageSourceName)),
                        default
                    );
                }
            }
        }


        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Promote ",
                    new Hilite(config[nameof(PackageName)]),
                    " ",
                    new Hilite(config[nameof(PackageVersion)])
                ),
                new RichDescription(
                    "from ",
                    new Hilite(config[nameof(PackageSourceName)]),
                    " to ",
                    new Hilite(config[nameof(TargetFeedName)])
                )
            );
        }
    }
}
