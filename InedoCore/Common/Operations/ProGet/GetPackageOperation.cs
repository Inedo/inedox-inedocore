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
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensions.Credentials;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
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
        [Required]
        [Persistent]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        public string PackageName { get; set; }
        [Persistent]
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [Description("The version of the package. Use \"latest\" to ensure the latest available version.")]
        public string PackageVersion { get; set; }
        [Required]
        [Persistent]
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory path on disk of the package contents.")]
        public string TargetDirectory { get; set; }
        [Required]
        [Persistent]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet feed URL")]
        [Description("The ProGet feed API endpoint URL.")]
        [MappedCredential(nameof(ProGetCredentials.Url))]
        public string FeedUrl { get; set; }
        [Persistent]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access this feed.")]
        [MappedCredential(nameof(ProGetCredentials.UserName))]
        public string UserName { get; set; }
        [Persistent]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [Description("The password of a user in ProGet that can access this feed.")]
        [MappedCredential(nameof(ProGetCredentials.Password))]
        public string Password { get; set; }
        [Persistent]
        public bool Current { get; set; }

        [Persistent]
        [Category("Identity")]
        [ScriptAlias("Credentials")]
        [DisplayName("Otter credentials")]
        [Description("The Otter credential name to use. If a credential name is specified, the UserName and Password fields will be ignored.")]
        public string CredentialName { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            var client = new ProGetClient(this.FeedUrl, this.UserName, this.Password);

            var packageId = ParseName(this.PackageName);

            this.LogInformation($"Connecting to {this.FeedUrl} to get metadata for {this.PackageName}...");
            var packageInfo = await client.GetPackageInfoAsync(packageId.Item1, packageId.Item2).ConfigureAwait(false);

            var version = new ProGetPackageVersionSpecifier(this.PackageVersion).GetBestMatch(packageInfo.versions);
            if (version == null)
            {
                this.LogError($"Package {this.PackageName} does not have a version {this.PackageVersion}.");
                return;
            }

            this.LogInformation($"Resolved package version is {version}.");

            this.LogInformation("Downloading package...");
            using (var zip = await client.DownloadPackageAsync(packageId.Item1, packageId.Item2, version).ConfigureAwait(false))
            {
                var dirsCreated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                await fileOps.CreateDirectoryAsync(this.TargetDirectory).ConfigureAwait(false);
                dirsCreated.Add(this.TargetDirectory);

                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase) || entry.Length <= "package/".Length)
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
                            await sourceStream.CopyToAsync(targetStream);
                        }

                        // timestamp in zip file is stored as UTC by convention
                        await fileOps.SetLastWriteTimeAsync(targetPath, entry.LastWriteTime.DateTime).ConfigureAwait(false);
                    }
                }
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

        private static Tuple<string, string> ParseName(string fullName)
        {
            fullName = fullName?.Trim('/');
            if (string.IsNullOrEmpty(fullName))
                return Tuple.Create(string.Empty, fullName);

            int index = fullName.LastIndexOf('/');

            if (index > 0)
                return Tuple.Create(fullName.Substring(0, index), fullName.Substring(index + 1));
            else
                return Tuple.Create(string.Empty, fullName);
        }
    }
}
