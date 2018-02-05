using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.Web;
using Inedo.Web.Plans.ArgumentEditors;

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Get Package")]
    [Description("Downloads the contents of a ProGet package to a specified directory.")]
    [ScriptAlias("Get-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("proget")]
#pragma warning disable CS0618 // Type or member is obsolete
    public sealed class GetPackageOperation : ExecuteOperation, IHasCredentials<ProGetCredentials>, IProGetPackageInstallTemplate
#pragma warning restore CS0618 // Type or member is obsolete
        , IHasCredentials<InedoProductCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

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
        [FilePathEditor]
        public string TargetDirectory { get; set; }

        [ScriptAlias("DeleteExtra")]
        [DisplayName("Delete files not in Package")]
        public bool DeleteExtra { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Server")]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from credential")]
#pragma warning disable CS0618 // Type or member is obsolete
        [MappedCredential(nameof(ProGetCredentials.Url))]
#pragma warning restore CS0618 // Type or member is obsolete
        public string FeedUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access the specified feed.")]
        [PlaceholderText("Use user name from credential")]
#pragma warning disable CS0618 // Type or member is obsolete
        [MappedCredential(nameof(ProGetCredentials.UserName))]
#pragma warning restore CS0618 // Type or member is obsolete
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [Description("The password of a user in ProGet that can access the specified feed.")]
        [PlaceholderText("Use password from credential")]
#pragma warning disable CS0618 // Type or member is obsolete
        [MappedCredential(nameof(ProGetCredentials.Password))]
#pragma warning restore CS0618 // Type or member is obsolete
        public string Password { get; set; }

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
