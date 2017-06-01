using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Extensions.Configurations.ProGet;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Operations;
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
        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            var client = new ProGetClient(this.Template.FeedUrl, this.Template.FeedName, this.Template.UserName, this.Template.Password, this);

            try
            {
                var packageId = PackageName.Parse(this.Template.PackageName);

                this.LogInformation($"Connecting to {this.Template.FeedUrl} to get metadata for {this.Template.PackageName}...");
                var packageInfo = await client.GetPackageInfoAsync(packageId).ConfigureAwait(false);

                string version;

                if (!string.IsNullOrEmpty(this.Template.PackageVersion) && !string.Equals(this.Template.PackageVersion, "latest", StringComparison.OrdinalIgnoreCase))
                {
                    if (!packageInfo.versions.Contains(this.Template.PackageVersion, StringComparer.OrdinalIgnoreCase))
                    {
                        this.LogError($"Package {this.Template.PackageName} does not have a version {this.Template.PackageVersion}.");
                        return;
                    }

                    version = this.Template.PackageVersion;
                }
                else
                {
                    version = packageInfo.latestVersion;
                    this.LogInformation($"Latest version of {this.Template.PackageName} is {version}.");
                }

                var deployInfo = PackageDeploymentData.Create(context, this, "Deployed by Ensure-Package operation, see URL for more info.");

                this.LogInformation("Downloading package...");
                using (var zip = await client.DownloadPackageAsync(packageId, version, deployInfo).ConfigureAwait(false))
                {
                    var dirsCreated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    await fileOps.CreateDirectoryAsync(this.Template.TargetDirectory).ConfigureAwait(false);
                    dirsCreated.Add(this.Template.TargetDirectory);

                    if (this.Template.DeleteExtra)
                    {
                        var remoteFileList = await fileOps.GetFileSystemInfosAsync(this.Template.TargetDirectory, MaskingContext.IncludeAll).ConfigureAwait(false);

                        foreach (var file in remoteFileList)
                        {
                            var relativeName = file.FullName.Substring(this.Template.TargetDirectory.Length).Replace('\\', '/').Trim('/');
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

                        var targetPath = fileOps.CombinePath(this.Template.TargetDirectory, relativeName);
                        if (relativeName.EndsWith("/"))
                        {
                            if (dirsCreated.Add(targetPath))
                                await fileOps.CreateDirectoryAsync(targetPath).ConfigureAwait(false);
                        }
                        else
                        {
                            var dir = PathEx.GetDirectoryName(targetPath);
                            if (dirsCreated.Add(dir))
                                await fileOps.CreateDirectoryAsync(dir);

                            using (var targetStream = await fileOps.OpenFileAsync(targetPath, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
                            using (var sourceStream = entry.Open())
                            {
                                await sourceStream.CopyToAsync(targetStream).ConfigureAwait(false);
                            }

                            await fileOps.SetLastWriteTimeAsync(targetPath, entry.LastWriteTime.DateTime).ConfigureAwait(false);
                        }
                    }
                }

                this.RecordServerPackageInfo(context, packageId.ToString(), version, client.GetViewPackageUrl(packageId, version));
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

        partial void RecordServerPackageInfo(IOperationExecutionContext context, string name, string version, string url);
    }
}
