using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SuggestionProviders;
using Inedo.IO;
using Inedo.UPack.Packaging;
using Inedo.Web;
using Inedo.Web.Plans.ArgumentEditors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [Tag("proget")]
    [ScriptAlias("Push-PackageFile")]
    [DefaultProperty(nameof(FilePath))]
    [ScriptNamespace(Namespaces.ProGet)]
    [DisplayName("Push Universal Package File (Preview)")]
    [Description("Uploads a universal package file to a package source.")]
    [Example(@"# Uploads the MyPackage.1.0.0.upack file to the InternalFeed package source
ProGet::Push-PackageFile MyPackage.1.0.0.upack
(
    PackageSource: InternalFeed
);")]
    public sealed class PushPackageFileOperation : RemotePackageOperationBase
    {
        private string userName;
        private string password;
        private string feedUrl;

        [Required]
        [ScriptAlias("FilePath")]
        [DisplayName("Package file path")]
        [FilePathEditor(IncludeFiles = true)]
        public string FilePath { get; set; }

        [Required]
        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(PackageSourceSuggestionProvider))]
        public override string PackageSource { get; set; }

        public override string PackageName { get => null; set => throw new InvalidOperationException(); }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var fullPath = context.ResolvePath(this.FilePath);
            if (!FileEx.Exists(fullPath))
                throw new ExecutionFailureException($"Package file {fullPath} does not exist.");

            this.LogDebug("Verifying package...");
            (var fullName, var version, bool vpack) = GetPackageInfo(fullPath);
            this.LogDebug($"Package verified. Name: {fullName}, Version: {version}");

            byte[] hash;
            if (!vpack)
                hash = await this.UploadAndComputeHashAsync(fullPath, this.feedUrl, this.userName, AH.CreateSecureString(this.password), context.CancellationToken);
            else
                hash = await this.UploadVirtualAndComputeHashAsync(fullPath, this.feedUrl, this.userName, AH.CreateSecureString(this.password), context.CancellationToken);

            return new PackageInfo(fullName, version, hash);
        }

        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            await base.AfterRemoteExecuteAsync(result);

            if (this.PackageManager != null && result is PackageInfo info)
            {
                this.LogDebug("Attaching package to build...");
                await this.PackageManager.AttachPackageToBuildAsync(
                    new AttachedPackage(AttachedPackageType.Universal, info.Name, info.Version, info.Hash, this.PackageSource),
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
                    new Hilite(config[nameof(PackageSource)])
                )
            );
        }

        private protected override void SetPackageSourceProperties(string userName, string password, string feedUrl)
        {
            this.userName = userName;
            this.password = password;
            this.feedUrl = feedUrl;
        }

        private static (string fullName, string version, bool vpack) GetPackageInfo(string path)
        {
            if (path.EndsWith(".vpack", StringComparison.OrdinalIgnoreCase))
            {
                using (var reader = new JsonTextReader(File.OpenText(path)))
                {
                    var obj = JObject.Load(reader);
                    var group = (string)obj.Property("group");
                    var name = (string)obj.Property("name");
                    var version = (string)obj.Property("version");
                    if (string.IsNullOrWhiteSpace(name))
                        throw new ExecutionFailureException($"{path} is not a valid virtual package file: missing \"name\" property.");
                    if (string.IsNullOrWhiteSpace(version))
                        throw new ExecutionFailureException($"{path} is not a valid virtual package file: missing \"version\" property.");

                    return (GetFullPackageName(group, name), version, true);
                }
            }

            try
            {
                using (var package = new UniversalPackage(path))
                {
                    return (GetFullPackageName(package.Group, package.Name), package.Version.ToString(), false);
                }
            }
            catch (Exception ex)
            {
                throw new ExecutionFailureException($"{path} is not a valid universal package file: {ex.Message}");
            }
        }

        [Serializable]
        private sealed class PackageInfo
        {
            public PackageInfo(string name, string version, byte[] hash)
            {
                this.Name = name;
                this.Version = version;
                this.Hash = hash;
            }

            public string Name { get; }
            public string Version { get; }
            public byte[] Hash { get; }
        }
    }
}
