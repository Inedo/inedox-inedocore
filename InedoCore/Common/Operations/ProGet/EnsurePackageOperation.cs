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
using Inedo.Otter.Extensibility.Configurations;
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
    public sealed class EnsurePackageOperation : EnsureOperation<ProGetPackageConfiguration>
    {
#if Otter
        public override async Task<PersistedConfiguration> CollectAsync(IOperationExecutionContext context)
        {
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            var client = new ProGetClient(this.Template.FeedUrl, this.Template.FeedName, this.Template.UserName, this.Template.Password, this);

            var packageId = PackageName.Parse(this.Template.PackageName);

            var packageInfo = await client.GetPackageInfoAsync(packageId).ConfigureAwait(false);

            var version = new ProGetPackageVersionSpecifier(this.Template.PackageVersion).GetBestMatch(packageInfo.versions);
            if (version == null)
            {
                this.LogError($"Package {this.Template.PackageName} does not have a version {this.Template.PackageVersion}.");
                return null;
            }

            this.LogInformation($"Resolved package version is {version}.");

            if (!await fileOps.DirectoryExistsAsync(this.Template.TargetDirectory).ConfigureAwait(false))
            {
                this.LogInformation(this.Template.TargetDirectory + " does not exist.");
                return new ProGetPackageConfiguration
                {
                    TargetDirectory = this.Template.TargetDirectory
                };
            }

            this.LogInformation(this.Template.TargetDirectory + " exists; getting remote file list...");

            var remoteFileList = await fileOps.GetFileSystemInfosAsync(this.Template.TargetDirectory, MaskingContext.IncludeAll).ConfigureAwait(false);

            var remoteFiles = new Dictionary<string, SlimFileSystemInfo>(remoteFileList.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var file in remoteFileList)
            {
                var relativeName = file.FullName.Substring(this.Template.TargetDirectory.Length).Replace('\\', '/').Trim('/');
                if (file is SlimDirectoryInfo)
                    relativeName += "/";

                remoteFiles.Add(relativeName, file);
            }

            remoteFileList = null; // async GC optimization

            this.LogDebug($"{this.Template.TargetDirectory} contains {remoteFiles.Count} file system entries.");

            this.LogInformation($"Connecting to {this.Template.FeedUrl} to get metadata for {this.Template.PackageName}:{version}...");
            var versionInfo = await client.GetPackageVersionInfoAsync(packageId, version).ConfigureAwait(false);
            if (versionInfo.fileList == null)
            {
                this.LogError("File list is unavailable for this package; it may be an orphaned entry.");
                return null;
            }

            this.LogDebug($"Package contains {versionInfo.fileList.Length} file system entries.");

            foreach (var entry in versionInfo.fileList)
            {
                var relativeName = entry.name;
                var file = remoteFiles.GetValueOrDefault(relativeName);
                if (file == null)
                {
                    this.LogInformation($"Entry {relativeName} is not present in {this.Template.TargetDirectory}.");
                    return new ProGetPackageConfiguration
                    {
                        TargetDirectory = this.Template.TargetDirectory
                    };
                }

                if (!entry.name.EndsWith("/"))
                {
                    var fileInfo = (SlimFileInfo)file;
                    if (entry.size != fileInfo.Size || entry.date != fileInfo.LastWriteTimeUtc)
                    {
                        this.LogInformation($"File {relativeName} in {this.Template.TargetDirectory} is different from file in package.");
                        this.LogDebug($"Source info: {entry.size} bytes, {entry.date} timestamp");
                        this.LogDebug($"Target info: {fileInfo.Size} bytes, {fileInfo.LastWriteTimeUtc} timestamp");
                        return new ProGetPackageConfiguration
                        {
                            TargetDirectory = this.Template.TargetDirectory
                        };
                    }
                }
            }

            if (this.Template.DeleteExtra)
            {
                foreach (var name in remoteFiles.Keys)
                {
                    if (!versionInfo.fileList.Any(entry => entry.name == name))
                    {
                        this.LogInformation($"File {name} in {this.Template.TargetDirectory} does not exist in package.");
                        return new ProGetPackageConfiguration
                        {
                            TargetDirectory = this.Template.TargetDirectory
                        };
                    }
                }
            }

            this.LogInformation($"All package files and directories are present in {this.Template.TargetDirectory}.");
            return new ProGetPackageConfiguration
            {
                Current = true,
                TargetDirectory = this.Template.TargetDirectory
            };
        }
#endif

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
            }
            catch (ProGetException ex)
            {
                this.LogError(ex.FullMessage);
                return;
            }

            this.LogInformation("Package deployed!");
        }

#if Otter
        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            var config = (ProGetPackageConfiguration)other;
            if (config?.Current != true)
                return new ComparisonResult(new[] { new Difference("Current", true, false) });
            else
                return new ComparisonResult(Enumerable.Empty<Difference>());
        }
#endif

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            object[] versionText;
            if (string.IsNullOrWhiteSpace(config[nameof(ProGetPackageConfiguration.PackageVersion)]))
                versionText = new object[] { new Hilite("latest version") };
            else
                versionText = new object[] { "version ", new Hilite(config[nameof(ProGetPackageConfiguration.PackageVersion)]) };

            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure ProGet package contents of ",
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
