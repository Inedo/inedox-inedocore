using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SuggestionProviders;
using Inedo.IO;
using Inedo.UPack;
using Inedo.UPack.Net;
using Inedo.UPack.Packaging;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [Tag("proget")]
    [ScriptAlias("Install-Package")]
    [DisplayName("Install Universal Package (Preview)")]
    [Description("Installs a universal package to the specified location using a Package Source.")]
    [ScriptNamespace(Namespaces.ProGet)]
    [AppliesTo(InedoProduct.BuildMaster)]
    public sealed class InstallPackageOperation : RemotePackageOperationBase
    {
        private string userName;
        private string password;
        private string feedUrl;

        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [PlaceholderText("Infer from package name")]
        [SuggestableValue(typeof(UniversalPackageSourceSuggestionProvider))]
        public override string PackageSource { get; set; }

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameFromSourceSuggestionProvider))]
        public override string PackageName { get; set; }

        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("attached version")]
        public string PackageVersion { get; set; }

        [ScriptAlias("To")]
        [DisplayName("To")]
        [PlaceholderText("$WorkingDirectory")]
        public string TargetDirectory { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.PackageSource))
                throw new ExecutionFailureException("Missing required argument: PackageSource");
            if (string.IsNullOrWhiteSpace(this.PackageName))
                throw new ExecutionFailureException("Missing required argument: Name");

            await base.BeforeRemoteExecuteAsync(context);

            if (this.PackageManager == null)
                throw new ExecutionFailureException("This operation requires package source support (BuildMaster 6.1.11 or later).");

            if (string.IsNullOrWhiteSpace(this.PackageVersion))
            {
                var match = (await this.PackageManager.GetBuildPackagesAsync(context.CancellationToken))
                    .FirstOrDefault(p => p.Active && p.PackageType == AttachedPackageType.Universal && string.Equals(p.Name, this.PackageName, StringComparison.OrdinalIgnoreCase) && string.Equals(p.PackageSource, this.PackageSource, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                    throw new ExecutionFailureException($"The current build has no active attached packages named {this.PackageName} from source {this.PackageSource}.");

                this.LogDebug($"Package version from attached package {this.PackageName} (source {this.PackageSource}): {match.Version}");
                this.PackageVersion = match.Version;
            }
        }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var client = new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(this.feedUrl), this.userName, AH.CreateSecureString(this.password)));

            var packageVersion = await client.GetPackageVersionAsync(UniversalPackageId.Parse(this.PackageName), UniversalPackageVersion.Parse(this.PackageVersion), false, context.CancellationToken);
            if (packageVersion == null)
            {
                this.LogError($"Package {this.PackageName} v{this.PackageVersion} not found on {this.PackageSource}.");
                return null;
            }

            long size = packageVersion.Size == 0 ? 100 * 1024 * 1024 : packageVersion.Size;

            var targetDir = context.ResolvePath(this.TargetDirectory);
            this.LogDebug($"Ensuring target directory ({targetDir}) exists...");
            DirectoryEx.Create(targetDir);

            this.LogInformation("Downloading package...");
            using (var tempStream = TemporaryStream.Create(size))
            {
                using (var sourceStream = await client.GetPackageStreamAsync(UniversalPackageId.Parse(this.PackageName), UniversalPackageVersion.Parse(this.PackageVersion), context.CancellationToken))
                {
                    await sourceStream.CopyToAsync(tempStream, 80 * 1024, context.CancellationToken);
                }

                tempStream.Position = 0;

                this.LogInformation($"Installing package to {targetDir}...");
                using (var package = new UniversalPackage(tempStream, true))
                {
                    await package.ExtractContentItemsAsync(targetDir, context.CancellationToken);
                }
            }

            this.LogInformation("Package installed.");
            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Install ",
                    new Hilite(config[nameof(PackageName)]),
                    " Universal Package"
                ),
                new RichDescription(
                    "to ",
                    new DirectoryHilite(config[nameof(TargetDirectory)])
                )
            );
        }

        private protected override void SetPackageSourceProperties(string userName, string password, string feedUrl)
        {
            this.userName = userName;
            this.password = password;
            this.feedUrl = feedUrl;
        }
    }
}
