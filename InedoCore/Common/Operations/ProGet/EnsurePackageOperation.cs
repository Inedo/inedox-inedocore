﻿using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.Configurations.ProGet;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Configurations;
using Inedo.Hedgehog.Extensibility.Credentials;
using Inedo.Hedgehog.Extensibility.Operations;
using Inedo.Hedgehog.Extensibility.RaftRepositories;
using Inedo.Hedgehog.Web;
using Inedo.Hedgehog.Web.Controls;
using Inedo.Hedgehog.Web.Controls.Plans;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Ensure Package")]
    [Description("Ensures that the contents of a ProGet package are in the specified directory.")]
    [ScriptAlias("Ensure-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("proget")]
    public sealed partial class EnsurePackageOperation : EnsureOperation<ProGetPackageConfiguration>
    {
        public override Task ConfigureAsync(IOperationExecutionContext context) => PackageDeployer.DeployAsync(context, this.Template, this, "Ensure-Package", true);

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            object[] versionText;
            if (string.IsNullOrWhiteSpace(config[nameof(ProGetPackageConfiguration.PackageVersion)]))
                versionText = new object[] { new Hilite("latest version") };
            else
                versionText = new object[] { "version ", new Hilite(config[nameof(ProGetPackageConfiguration.PackageVersion)]) };

            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure universal package contents of ",
                    versionText,
                    " of ",
                    new Hilite(config[nameof(ProGetPackageConfiguration.PackageName)])
                ),
                new RichDescription(
                    "are present in ",
                    new DirectoryHilite(config[nameof(ProGetPackageConfiguration.TargetDirectory)])
                )
            );
        }
    }
}
