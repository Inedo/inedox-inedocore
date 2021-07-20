using System;
using System.ComponentModel;
using System.IO;
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
using Inedo.UPack.Packaging;
using Inedo.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [Tag("proget")]
    [ScriptAlias("Push-PackageFile")]
    [DefaultProperty(nameof(FilePath))]
    [ScriptNamespace(Namespaces.ProGet)]
    [DisplayName("Push Universal Package File")]
    [Description("Uploads a universal package file to a package source.")]
    [Example(@"# Uploads the MyPackage.1.0.0.upack file to the InternalFeed package source
ProGet::Push-PackageFile MyPackage.1.0.0.upack
(
    PackageSource: InternalFeed
);")]
    public sealed class PushPackageFileOperation : RemoteExecuteOperation, IFeedConfiguration
    {
        [Required]
        [ScriptAlias("FilePath")]
        [DisplayName("Package file path")]
        public string FilePath { get; set; }

        [ScriptAlias("To")]
        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public string PackageSourceName { get; set; }

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

        [NonSerialized]
        private IPackageManager packageManager;
        [NonSerialized]
        private string originalPackageSourceName;

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            this.originalPackageSourceName = this.PackageSourceName;
            this.packageManager = await context.TryGetServiceAsync<IPackageManager>();
            this.PrepareCredentialPropertiesForRemote(context);
        }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var fullPath = context.ResolvePath(this.FilePath);
            if (!FileEx.Exists(fullPath))
                throw new ExecutionFailureException($"Package file {fullPath} does not exist.");

            this.LogDebug("Verifying package...");
            (var fullName, var version, bool vpack) = GetPackageInfo(fullPath);
            this.LogDebug($"Package verified. Name: {fullName}, Version: {version}");

            var client = this.TryCreateProGetFeedClient(this, context.CancellationToken);
            byte[] hash;
            if (vpack)
                hash = await client.UploadVirtualPackageAndComputeHashAsync(fullPath);
            else
                hash = await client.UploadPackageAndComputeHashAsync(fullPath);

            return new PackageInfo(fullName, version, hash);
        }

        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            await base.AfterRemoteExecuteAsync(result);

            if (this.packageManager != null && result is PackageInfo info && !string.IsNullOrWhiteSpace(this.originalPackageSourceName))
            {
                this.LogDebug("Attaching package to build...");
                await this.packageManager.AttachPackageToBuildAsync(
                    new AttachedPackage(AttachedPackageType.Universal, info.Name, info.Version, info.Hash, this.originalPackageSourceName),
                    default
                );
                this.LogDebug("Package attached.");
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Push Universal Package ",
                    new DirectoryHilite(config[nameof(FilePath)])
                ),
                new RichDescription(
                    "to ",
                    new Hilite(config[nameof(PackageSourceName)])
                )
            );
        }
        private static (string fullName, string version, bool vpack) GetPackageInfo(string path)
        {
            if (path.EndsWith(".vpack", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new JsonTextReader(File.OpenText(path));
                var obj = JObject.Load(reader);
                var group = (string)obj.Property("group");
                var name = (string)obj.Property("name");
                var version = (string)obj.Property("version");
                if (string.IsNullOrWhiteSpace(name))
                    throw new ExecutionFailureException($"{path} is not a valid virtual package file: missing \"name\" property.");
                if (string.IsNullOrWhiteSpace(version))
                    throw new ExecutionFailureException($"{path} is not a valid virtual package file: missing \"version\" property.");

                return (new UniversalPackageId(group, name).ToString(), version, true);
            }

            try
            {
                using var package = new UniversalPackage(path);
                return (new UniversalPackageId(package.Group, package.Name).ToString(), package.Version.ToString(), false);
            }
            catch (Exception ex)
            {
                throw new ExecutionFailureException($"{path} is not a valid universal package file: {ex.Message}");
            }
        }

        [Serializable]
        [SlimSerializable]
        private sealed class PackageInfo
        {
            public PackageInfo()
            {
            }
            public PackageInfo(string name, string version, byte[] hash)
            {
                this.Name = name;
                this.Version = version;
                this.Hash = hash;
            }

            [SlimSerializable]
            public string Name { get; set; }
            [SlimSerializable]
            public string Version { get; set; }
            [SlimSerializable]
            public byte[] Hash { get; set; }
        }
    }
}
