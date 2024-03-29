﻿using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;

namespace Inedo.Extensions.Operations.ProGet.Packages
{
    [Tag("proget")]
    [ScriptAlias("Promote")]
    [ScriptAlias("Promote-Package", Obsolete = true)]
    [DisplayName("Promote Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Description("Promotes a package from one feed to another in a ProGet instance.")]
    public sealed class PromoteOperation : ExecuteOperation, IFeedPackageConfiguration
    {
        [ScriptAlias("PackageSource")]
        [ScriptAlias("From", Obsolete = true)]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(RepackPromoteSourceSuggestionProvider))]
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
        public string FeedName { get; set; }

        [ScriptAlias("EndpointUrl")]
        [DisplayName("API endpoint URL")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use URL from package source")]
        public string ApiUrl { get; set; }

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
        [Undisclosed]
        [ScriptAlias("FeedUrl")]
        public string FeedUrl { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrEmpty(this.PackageGroup))
                this.PackageName = this.PackageGroup + "/" + this.PackageName;

            await this.EnsureProGetConnectionInfoAsync(context, context.CancellationToken);

            var client = new ProGetFeedClient(this, this);

            if (string.IsNullOrEmpty(this.TargetFeedName) && !string.IsNullOrEmpty(this.TargetPackageSourceName))
            {
                string targetApiEndpointUrl;
                var targetPackageSource = SecureResource.TryCreate(SecureResourceType.General, this.TargetPackageSourceName, context as ICredentialResolutionContext ?? CredentialResolutionContext.None);
                if (targetPackageSource is UniversalPackageSource utargetPackageSource)
                    targetApiEndpointUrl = utargetPackageSource.ApiEndpointUrl;
                else if (targetPackageSource is NuGetPackageSource ntargetPackageSource)
                    targetApiEndpointUrl = ntargetPackageSource.ApiEndpointUrl;
                else
                    throw new ExecutionFailureException($"No {nameof(UniversalPackageSource)} or {nameof(NuGetPackageSource)} with the name {this.TargetPackageSourceName} was found.");

                if (!Extensions.TryParseFeedUrl(targetApiEndpointUrl, out _, out _, out var targetFeedName))
                    throw new ExecutionFailureException("Target Package Source does have the same feed type and base url as From package source.");

                this.TargetFeedName = targetFeedName;
            }

            if (string.IsNullOrEmpty(this.TargetFeedName))
                throw new ExecutionFailureException("ToFeed is required.");

            await this.ResolveAttachedPackageAsync(context);
            var packageManager = await context.TryGetServiceAsync<IPackageManager>();

            this.LogInformation($"Promoting {this.PackageName} {this.PackageVersion} to {this.TargetFeedName}...");

            if (string.Equals(this.TargetFeedName, this.FeedName, StringComparison.OrdinalIgnoreCase))
            {
                this.LogWarning("Target feed and source feed are the same; nothing to do.");
                return;
            }

            await client.PromoteAsync(this.PackageName, this.PackageVersion, this.TargetFeedName, this.Reason, context.CancellationToken);

            if (packageManager != null && !string.IsNullOrWhiteSpace(this.PackageSourceName))
            {
                AttachedPackage package = null;

                foreach (var p in await packageManager.GetBuildPackagesAsync(default))
                {
                    if (p.Active && string.Equals(p.Name, this.PackageName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.Version, this.PackageVersion, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.PackageSource, this.PackageSourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        package = p;
                        await packageManager.DeactivatePackageAsync(p.Name, p.Version, p.PackageSource);
                    }
                }

                if (package != null)
                {
                    await packageManager.AttachPackageToBuildAsync(
                        new AttachedPackage(package.PackageType, package.Name, package.Version, package.Hash, AH.CoalesceString(this.TargetPackageSourceName, this.PackageSourceName)),
                        context.CancellationToken
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
