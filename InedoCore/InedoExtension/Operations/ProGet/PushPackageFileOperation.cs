using System;
using System.ComponentModel;
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

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var fullPath = context.ResolvePath(this.FilePath);
            if (!FileEx.Exists(fullPath))
                throw new ExecutionFailureException($"Package file {fullPath} does not exist.");

            string group;
            string name;
            string version;

            this.LogDebug("Verifying package...");

            using (var package = openPackage(fullPath))
            {
                group = package.Group;
                name = package.Name;
                version = package.Version.ToString();
            }

            this.LogDebug($"Package verfied. Name: {GetFullPackageName(group, name)}, Version: {version}");

            var hash = await this.UploadAndComputeHashAsync(fullPath, this.feedUrl, this.userName, AH.CreateSecureString(this.password), context.CancellationToken);

            return new PackageInfo(GetFullPackageName(group, name), version, hash);

            UniversalPackage openPackage(string path)
            {
                try
                {
                    return new UniversalPackage(path);
                }
                catch (Exception ex)
                {
                    throw new ExecutionFailureException($"{path} is not a valid universal package file: {ex.Message}");
                }
            }
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
