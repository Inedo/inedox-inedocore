using Inedo.Extensions.PackageSources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;

namespace Inedo.Extensions.Operations.ProGet.Packages
{
    [Tag("proget")]
    [ScriptAlias("Install-Package")]
    [DisplayName("Install Universal Package")]
    [Description("Installs a universal package to the specified location using a Package Source.")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Example(@"ProGet::Install-Package
(
    From: MyPackageSource,
    Name: MyAppPackage,
    Version: 3.4.2,
    To: C:\MyApps\MyApp
);
")]
    public sealed class InstallUniversalPackageOperation : RemoteExecuteOperation, IFeedPackageInstallationConfiguration
    {
        [ScriptAlias("PackageSource")]
        [ScriptAlias("From", Obsolete = true)]
        [DisplayName("Package source")]
        [PlaceholderText("Infer from package name")]
        [SuggestableValue(typeof(UniversalPackageSourceSuggestionProvider))]
        public string PackageSourceName { get; set; }

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }

        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("\"latest\" (Otter) or \"attached\" (BuildMaster)")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [ScriptAlias("To")]
        [DisplayName("Target directory")]
        [PlaceholderText("$WorkingDirectory")]
        public string TargetDirectory { get; set; }

        [Category("Local registry")]
        [ScriptAlias("LocalRegistry")]
        [DisplayName("Use Local Registry")]
        [PlaceholderText("Record installation in Machine registry")]
        [DefaultValue(LocalRegistryOptions.Machine)]
        public LocalRegistryOptions LocalRegistry { get; set; } = LocalRegistryOptions.Machine;

        [Category("Local registry")]
        [Description("Cache Package")]
        [ScriptAlias("LocalCache")]
        [DefaultValue(false)]
        [PlaceholderText("package is not cached locally")]
        public bool LocalCache { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("DirectDownload")]
        [DisplayName("Direct download")]
        [PlaceholderText("download package file on remote server")]
        [Description("Set this to value to false if your remote server doesn't have direct access to the ProGet feed.")]
        [DefaultValue(true)]
        public bool DirectDownload { get; set; }

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
        [ScriptAlias("FeedUrl")]
        public string FeedUrl { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.PackageVersion))
            {
                if (SDK.ProductName == "BuildMaster")
                    this.PackageVersion = "attached";
                else
                    this.PackageVersion = "latest";
            }

            await this.EnsureProGetConnectionInfoAsync(context, context.CancellationToken);
            await this.ResolveAttachedPackageAsync(context);
        }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            await this.InstallPackageAsync(this, context.ResolvePath(this.TargetDirectory), context.CancellationToken);
            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Install Universal Package ",
                    new Hilite(config[nameof(IFeedPackageInstallationConfiguration.PackageName)]),
                    $" ({AH.CoalesceString(config[nameof(IFeedPackageInstallationConfiguration.PackageVersion)], "latest")})."
                ),
                new RichDescription(
                    " to ",
                    new DirectoryHilite(config[nameof(IFeedPackageInstallationConfiguration.TargetDirectory)])
                )
            );
        }
    }
}
