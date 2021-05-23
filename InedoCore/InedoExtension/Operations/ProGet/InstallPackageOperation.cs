
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.IO;
using Inedo.Serialization;
using Inedo.UPack;
using Inedo.UPack.Net;
using Inedo.UPack.Packaging;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [Tag("proget")]
    [ScriptAlias("Install-Package")]
    [DisplayName("Install Universal Package")]
    [Description("Installs a universal package to the specified location using a Package Source.")]
    [ScriptNamespace(Namespaces.ProGet)]
    public sealed class InstallPackageOperation : RemotePackageOperationBase, IFeedPackageConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [PlaceholderText("Infer from package name")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public override string PackageSource { get; set; }
        string IFeedPackageConfiguration.PackageSourceName => this.PackageSource;

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameFromSourceSuggestionProvider))]
        public override string PackageName { get; set; }

        [ScriptAlias("Version")]
        [DisplayName("Package version")]
#warning conditional placeholder text?
        [PlaceholderText("attached version")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [ScriptAlias("To")]
        [DisplayName("Target directory")]
        [PlaceholderText("$WorkingDirectory")]
        public string TargetDirectory { get; set; }

        [Category("Local registry")]
        [ScriptAlias("LocalRegistry")]
        [DisplayName("Use Local Registry")]
        [Description("See https://docs.inedo.com/docs/upack-universal-package-registry-what-is")]
        [DefaultValue("None")]
        [SuggestableValue("Machine", "User", /*"Custom", */ "None")]
        [Persistent]
        public string LocalRegistry { get; set; }

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
        [SuggestableValue(typeof(FeedNameSuggestionProvider))]
        public string FeedName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from package source")]
        [SlimSerializable]
        public string FeedUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access this feed.")]
        [PlaceholderText("Use user name from package source")]
        [SlimSerializable] 
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [PlaceholderText("Use password from package source")]
        [Description("The password of a user in ProGet that can access this feed.")]
        [SlimSerializable] 
        public string Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiKey")]
        [DisplayName("ProGet API Key")]
        [PlaceholderText("Use API Key from package source")]
        [Description("An API Key that can access this feed.")]
        public string ApiKey { get; set; }
        
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
            var client = new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(this.FeedUrl), this.UserName, AH.CreateSecureString(this.Password)));

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
            this.UserName = userName;
            this.Password = password;
            this.FeedUrl = feedUrl;
        }
    }
}
