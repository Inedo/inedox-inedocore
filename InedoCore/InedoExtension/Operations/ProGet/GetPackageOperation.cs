using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.Web;
using Inedo.Web.Plans.ArgumentEditors;

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Get Package (Deprecated)")]
    [Description("Downloads the contents of a ProGet package to a specified directory.")]
    [ScriptAlias("Get-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Note("This has been deprecated in favor of Ensure-Package and Install-Package")]
    [Undisclosed]
    [Tag("proget")]
    public sealed partial class GetPackageOperation : ExecuteOperation, IFeedPackageConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public string PackageSourceName { get; set; }

        [Required]
        [ScriptAlias("Feed")]
        [DisplayName("Feed name")]
        [SuggestableValue(typeof(FeedNameSuggestionProvider))]
        public string FeedName { get; set; }

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }
        
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("latest")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Required]
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory path on disk of the package contents.")]
        public string TargetDirectory { get; set; }

        [ScriptAlias("DeleteExtra")]
        [DisplayName("Delete files not in Package")]
        public bool DeleteExtra { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Server")]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from credential")]
        public string FeedUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access the specified feed.")]
        [PlaceholderText("Use user name from credential")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [Description("The password of a user in ProGet that can access the specified feed.")]
        [PlaceholderText("Use password from credential")]
        public string Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiKey")]
        [DisplayName("ProGet API Key")]
        [PlaceholderText("Use API Key from package source")]
        [Description("An API Key that can access this feed.")]
        public string ApiKey { get; set; }

        [Category("Advanced")]
        [ScriptAlias("RecordDeployment")]
        [DisplayName("Record deployment in ProGet")]
        [DefaultValue(true)]
        public bool RecordDeployment { get; set; } = true;

        private volatile OperationProgress progress = null;
        public override OperationProgress GetProgress() => progress;

        public override Task ExecuteAsync(IOperationExecutionContext context) => PackageDeployer.DeployAsync(context, this, this, "Get-Package", this.RecordDeployment, p => this.progress = p);

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            object[] versionText;
            if (string.IsNullOrWhiteSpace(config[nameof(this.PackageVersion)]))
                versionText = new object[] { new Hilite("latest version") };
            else
                versionText = new object[] { "version ", new Hilite(config[nameof(this.PackageVersion)]) };

            return new ExtendedRichDescription(
                new RichDescription(
                    "Install universal package contents of ",
                    versionText,
                    " of ",
                    new Hilite(config[nameof(this.PackageName)])
                ),
                new RichDescription(
                    "to ",
                    new DirectoryHilite(config[nameof(this.TargetDirectory)])
                )
            );
        }
    }
}
