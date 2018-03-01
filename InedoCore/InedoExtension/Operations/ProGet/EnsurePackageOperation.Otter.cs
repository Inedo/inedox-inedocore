using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Extensions.Configurations.ProGet;
using Inedo.IO;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.ProGet
{
    partial class EnsurePackageOperation
    {
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            var client = new ProGetClient(this.Template.FeedUrl, this.Template.FeedName, this.Template.UserName, this.Template.Password, this);

            try
            {

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

                var mask = new MaskingContext(this.Template.Includes, this.Template.Excludes);

                this.LogInformation(this.Template.TargetDirectory + " exists; getting remote file list...");

                var remoteFileList = await fileOps.GetFileSystemInfosAsync(this.Template.TargetDirectory, mask).ConfigureAwait(false);

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
                    if (!mask.IsMatch(relativeName))
                    {
                        continue;
                    }

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
            catch (ProGetException ex)
            {
                this.LogError(ex.FullMessage);
                return null;
            }
        }

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            var config = (ProGetPackageConfiguration)other;
            if (config?.Current != true)
                return new ComparisonResult(new[] { new Difference("Current", true, false) });
            else
                return new ComparisonResult(Enumerable.Empty<Difference>());
        }
    }
}
