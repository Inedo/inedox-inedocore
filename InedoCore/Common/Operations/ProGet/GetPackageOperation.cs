using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Extensions;
using Inedo.Extensions.SuggestionProviders;
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
#endif
using Inedo.Serialization;

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Get Package")]
    [Description("Downloads the contents of a ProGet package to a specified directory.")]
    [ScriptAlias("Get-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("proget")]
    public sealed class GetPackageOperation : ExecuteOperation, IHasCredentials<ProGetCredentials>
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
#if BuildMaster
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
        public string Server { get; set; }

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

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            var client = new ProGetClient(this.Server, this.FeedName, this.UserName, this.Password, this);

            try
            {
                var packageId = ProGet.PackageName.Parse(this.PackageName);

                this.LogInformation($"Connecting to {this.Server} to get metadata for {this.PackageName}...");
                var packageInfo = await client.GetPackageInfoAsync(packageId).ConfigureAwait(false);

                var version = new ProGetPackageVersionSpecifier(this.PackageVersion).GetBestMatch(packageInfo.versions);
                if (version == null)
                {
                    this.LogError($"Package {this.PackageName} does not have a version {this.PackageVersion}.");
                    return;
                }

                this.LogInformation($"Resolved package version is {version}.");

                var deployInfo = PackageDeploymentData.Create(context, this, "Deployed by Get-Package operation, see URL for more info.");

                this.LogInformation("Downloading package...");
                using (var zip = await client.DownloadPackageAsync(packageId, version, deployInfo).ConfigureAwait(false))
                {
                    var dirsCreated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    this.LogDebug("Creating directory: " + this.TargetDirectory);
                    await fileOps.CreateDirectoryAsync(this.TargetDirectory).ConfigureAwait(false);
                    dirsCreated.Add(this.TargetDirectory);

                    if (this.DeleteExtra)
                    {
                        var remoteFileList = await fileOps.GetFileSystemInfosAsync(this.TargetDirectory, MaskingContext.IncludeAll).ConfigureAwait(false);

                        foreach (var file in remoteFileList)
                        {
                            var relativeName = file.FullName.Substring(this.TargetDirectory.Length).Replace('\\', '/').Trim('/');
                            var entry = zip.GetEntry("package/" + relativeName);
                            if (file is SlimDirectoryInfo)
                            {
                                if (entry == null || !entry.IsDirectory())
                                {
                                    this.LogDebug($"Deleting extra directory: {relativeName}");
                                    await fileOps.DeleteDirectoryAsync(file.FullName).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                if (entry == null || entry.IsDirectory())
                                {
                                    this.LogDebug($"Deleting extra file: {relativeName}");
                                    await fileOps.DeleteFileAsync(file.FullName).ConfigureAwait(false);
                                }
                            }
                        }
                    }

                    foreach (var entry in zip.Entries)
                    {
                        if (!entry.FullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase) || entry.FullName.Length <= "package/".Length)
                            continue;

                        var relativeName = entry.FullName.Substring("package/".Length);

                        var targetPath = fileOps.CombinePath(this.TargetDirectory, relativeName);
                        if (relativeName.EndsWith("/"))
                        {
                            if (dirsCreated.Add(targetPath))
                                await fileOps.CreateDirectoryAsync(targetPath).ConfigureAwait(false);
                        }
                        else
                        {
                            var dir = PathEx.GetDirectoryName(targetPath);
                            if (dirsCreated.Add(dir))
                                await fileOps.CreateDirectoryAsync(dir).ConfigureAwait(false);

                            using (var targetStream = await fileOps.OpenFileAsync(targetPath, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
                            using (var sourceStream = entry.Open())
                            {
                                await sourceStream.CopyToAsync(targetStream).ConfigureAwait(false);
                            }

                            // timestamp in zip file is stored as UTC by convention
                            await fileOps.SetLastWriteTimeAsync(targetPath, entry.LastWriteTime.DateTime).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (ProGetException ex)
            {
                this.LogError(ex.FullMessage);
                return;
            }

            this.LogInformation("Package deployed!");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            object[] versionText;
            if (string.IsNullOrWhiteSpace(config[nameof(this.PackageVersion)]))
                versionText = new object[] { new Hilite("latest version") };
            else
                versionText = new object[] { "version ", new Hilite(config[nameof(this.PackageVersion)]) };

            return new ExtendedRichDescription(
                new RichDescription(
                    "Install ProGet package contents of ",
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
