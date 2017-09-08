using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensions.Credentials;
using Inedo.Otter.Web.Controls;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Plans;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensibility.Web;
using Inedo.Extensibility.Web.Plans.ArgumentEditors;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Get Package")]
    [Description("Downloads the contents of a ProGet package to a specified directory.")]
    [ScriptAlias("Get-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("proget")]
    public sealed class GetPackageOperation : ExecuteOperation, IHasCredentials<ProGetCredentials>, IProGetPackageInstallTemplate
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Required]
        [ScriptAlias("Feed")]
        [DisplayName("Feed name")]
        [SuggestibleValue(typeof(FeedNameSuggestionProvider))]
        public string FeedName { get; set; }

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestibleValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }
        
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("latest")]
        [SuggestibleValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Required]
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory path on disk of the package contents.")]
#if BuildMaster || Hedgehog
        [FilePathEditor]
#endif
        public string TargetDirectory { get; set; }

        [ScriptAlias("DeleteExtra")]
        [DisplayName("Delete files not in Package")]
        public bool DeleteExtra { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Server")]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from credential")]
        [MappedCredential(nameof(ProGetCredentials.Url))]
        public string FeedUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access the specified feed.")]
        [PlaceholderText("Use user name from credential")]
        [MappedCredential(nameof(ProGetCredentials.UserName))]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [Description("The password of a user in ProGet that can access the specified feed.")]
        [PlaceholderText("Use password from credential")]
        [MappedCredential(nameof(ProGetCredentials.Password))]
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
