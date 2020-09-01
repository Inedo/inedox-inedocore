using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.IO;
using Inedo.UPack.Packaging;
using Inedo.Web;
using Inedo.Web.Plans.ArgumentEditors;

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Push Package")]
    [Description("Uploads a universal package to a ProGet feed.")]
    [ScriptAlias("Push-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("ProGet")]
    [Serializable]
    [Example(@"# Push ProfitCalc-$ReleaseNumber.upack to the ""ApplicationPackages"" package source
ProGet::Push-Package
(
    PackageSource: ApplicationPackages,
    FilePath: ProfitCalc-$ReleaseNumber.upack
);")]
    [Note("If uploading to a Package Source, use the ProGet::PushPackageFile operation instead.")]
    [SeeAlso(typeof(PushPackageFileOperation))]
#pragma warning disable CS0618 // Type or member is obsolete
    public sealed class PushPackageOperation : RemoteExecuteOperation, IHasCredentials<ProGetCredentials>
#pragma warning restore CS0618 // Type or member is obsolete
        , IHasCredentials<InedoProductCredentials>
    {
        [NonSerialized]
        private IPackageManager packageManager;

        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Required]
        [ScriptAlias("Feed")]
        [DisplayName("Feed name")]
        [SuggestableValue(typeof(FeedNameSuggestionProvider))]
        public string FeedName { get; set; }

        [Required]
        [ScriptAlias("FilePath")]
        [DisplayName("Package file path")]
        [FilePathEditor(IncludeFiles = true)]
        public string FilePath { get; set; }

        [ScriptAlias("Group")]
        [DisplayName("Group name")]
        [PlaceholderText("Ungrouped")]
        public string Group { get; set; }

        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        public string Name { get; set; }

        [ScriptAlias("Version")]
        public string Version { get; set; }

        [ScriptAlias("Description")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Description("The package description supports Markdown syntax.")]
        public string Description { get; set; }

        [Category("Advanced")]
        [ScriptAlias("Title")]
        public string Title { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Icon")]
        [Description("A string of an absolute url pointing to an image to be displayed in the ProGet UI (at both 64px and 128px); if  package:// is used as the protocol, ProGet will search within the package and serve that image instead")]
        public string Icon { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Dependencies")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Description(@"Dependencies should be supplied as a list, each consisting of a package identification string; this string is formatted as follows:
                    <ul>
                        <li>«group»:«package-name»</li>
                        <li>«group»:«package-name»:«version»</li>
                    </ul>
                    When the version is not specified, the latest is used.")]
        public IEnumerable<string> Dependencies { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Server")]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from credential")]
#pragma warning disable CS0618 // Type or member is obsolete
        [MappedCredential(nameof(ProGetCredentials.Url))]
#pragma warning restore CS0618 // Type or member is obsolete
        public string Server { get; set; }

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

        [Category("Connection/Identity")]
        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(PackageSourceSuggestionProvider))]
        public string PackageSource { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            await base.BeforeRemoteExecuteAsync(context);
            this.packageManager = await context.TryGetServiceAsync<IPackageManager>();

            // if username is not already specified and there is a package source, look up any attached credentials
            if (string.IsNullOrEmpty(this.UserName) && !string.IsNullOrEmpty(this.PackageSource))
            {
                this.LogDebug($"Using package source {this.PackageSource}.");
                var packageSource = (UniversalPackageSource)SecureResource.Create(this.PackageSource, (IResourceResolutionContext)context);

                if (!string.IsNullOrEmpty(packageSource.CredentialName))
                {
                    this.LogDebug($"Using credentials {packageSource.CredentialName}.");
                    var creds = packageSource.GetCredentials((ICredentialResolutionContext)context);
                    if (creds is TokenCredentials tc)
                    {
                        this.UserName = "api";
                        this.Password = AH.Unprotect(tc.Token);
                    }
                    else if (creds is Inedo.Extensions.Credentials.UsernamePasswordCredentials upc)
                    {
                        this.UserName = upc.UserName;
                        this.Password = AH.Unprotect(upc.Password);
                    }
                    else
                        throw new InvalidOperationException();
                }
            }
        }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var client = new ProGetClient(this.Server, this.FeedName, this.UserName, this.Password, this, context.CancellationToken);

            try
            {
                this.LogInformation($"Pushing package {this.Name} to ProGet...");

                string path = context.ResolvePath(this.FilePath);

                this.LogDebug("Using package file: " + path);

                if (!FileEx.Exists(path))
                {
                    this.LogError(this.FilePath + " does not exist.");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(this.Name) || string.IsNullOrWhiteSpace(this.Version))
                {
                    try
                    {
                        using (var package = new UniversalPackage(path))
                        {
                            if (string.IsNullOrWhiteSpace(package.Name) || package.Version == null)
                            {
                                this.LogError("Name and Version properties are required unless pushing a package that already has those properties set.");
                                return null;
                            }
                        }
                    }
                    catch
                    {
                        this.LogError("Name and Version properties are required unless pushing a package that already has those properties set.");
                        return null;
                    }
                }

                using (var file = FileEx.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var data = new ProGetPackagePushData
                    {
                        Title = this.Title,
                        Description = this.Description,
                        Icon = this.Icon,
                        Dependencies = this.Dependencies?.ToArray()
                    };

                    await client.PushPackageAsync(this.Group, this.Name, this.Version, data, file);
                }
            }
            catch (ProGetException ex)
            {
                this.LogError(ex.FullMessage);
                return null;
            }

            this.LogInformation("Package pushed.");
            return new PackageInfo(this.Name, this.Version);
        }

        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            if (this.packageManager != null && result is PackageInfo info && !string.IsNullOrWhiteSpace(this.PackageSource))
            {
                await this.packageManager.AttachPackageToBuildAsync(
                    new AttachedPackage(AttachedPackageType.Universal, info.PackageName, info.Version, null, this.PackageSource),
                    default
                );
            }

            await base.AfterRemoteExecuteAsync(result);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Push ", new Hilite(config[nameof(Name)]), " Package"),
                new RichDescription("to ProGet feed ", config[nameof(Server)])
            );
        }

        [Serializable]
        private sealed class PackageInfo
        {
            public PackageInfo(string packageName, string version)
            {
                this.PackageName = packageName;
                this.Version = version;
            }

            public string PackageName { get; }
            public string Version { get; }
        }
    }
}
