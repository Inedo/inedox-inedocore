using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Configurations.ProGet;
using Inedo.Extensions.UniversalPackages;
using Inedo.IO;
using Inedo.UPack.Packaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Ensure Universal Package")]
    [Description("Ensures that the specified universal package is installed in the specified directory.")]
    [ScriptAlias("Ensure-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("proget")]
    public sealed class EnsurePackageOperation : EnsureOperation<ProGetPackageConfiguration>
    {
        private volatile OperationProgress progress = null;

        public override OperationProgress GetProgress() => this.progress;
        internal void SetProgress(OperationProgress p) => this.progress = p;
        private void SetProgress(int? percent, string message) => this.progress = new OperationProgress(percent, message);

        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var client = ProGetFeedClient.TryCreate(this.Template, context as ICredentialResolutionContext ?? CredentialResolutionContext.None, this, context.CancellationToken);
            this.LogInformation($"Connecting to {client.ProGetBaseUrl} to get metadata for {this.Template.PackageName}:{this.Template.PackageVersion}...");

            var package = await client.FindPackageVersionAsync(this.Template.PackageName, this.Template.PackageVersion);
            if (package == null)
            {
                this.LogError($"Package {this.Template.PackageName} does not have a version {this.Template.PackageVersion}.");
                return null;
            }

            var collectedConfig = new ProGetPackageConfiguration
            {
                PackageName = package.FullName.ToString(),
                PackageVersion = package.Version.ToString()
            };

            // Check the registry
            if (this.Template.LocalRegistry != "None")
            {
                collectedConfig.LocalRegistry = this.Template.LocalRegistry;

                this.LogDebug($"Connecting to the {this.Template.LocalRegistry} package registry...");
                using var registry = await RemotePackageRegistry.GetRegistryAsync(context.Agent, false);

                this.LogDebug("Acquiring package registry lock...");
                await registry.LockAsync(context.CancellationToken);
                this.LogDebug($"Package registry lock acquired (token={registry.LockToken}).");

                this.LogInformation("Retrieving list of packages...");
                var installedPackages = await registry.GetInstalledPackagesAsync();
                this.LogInformation("Packages installed: " + installedPackages.Count);

                await registry.UnlockAsync().ConfigureAwait(false);

                var matchedInstalledPackage = installedPackages.FirstOrDefault(p =>
                    string.Equals(p.Name, package.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Group, package.Group, StringComparison.OrdinalIgnoreCase)
                );
                if (matchedInstalledPackage == null)
                {
                    this.LogInformation("Package not listed in registry.");
                    collectedConfig.Exists = false;
                    return collectedConfig;
                }
                if (!string.Equals(matchedInstalledPackage.Version, package.Version.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    collectedConfig.PackageVersion = matchedInstalledPackage.Version;
                    return collectedConfig;
                }

                this.LogInformation("Package listed in registry.");
                if (!this.Template.Exists)
                {
                    collectedConfig.TargetDirectory = matchedInstalledPackage.InstallPath;
                    return collectedConfig;
                }
            }


            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            try
            {
                if (!await fileOps.DirectoryExistsAsync(this.Template.TargetDirectory).ConfigureAwait(false))
                {
                    this.LogInformation(this.Template.TargetDirectory + " does not exist.");
                    collectedConfig.Exists = false;
                    return collectedConfig;
                }
                collectedConfig.TargetDirectory = this.Template.TargetDirectory;

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

                this.LogInformation($"Connecting to feed to get file metadata...");
                var versionInfo = await client.GetPackageVersionWithFilesAsync(package.FullName, package.Version);

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
                        collectedConfig.DriftedFiles = true;
                        return collectedConfig;
                    }

                    if (!entry.name.EndsWith("/"))
                    {
                        var fileInfo = (SlimFileInfo)file;
                        if (entry.size != fileInfo.Size || entry.date != fileInfo.LastWriteTimeUtc)
                        {
                            this.LogInformation($"File {relativeName} in {this.Template.TargetDirectory} is different from file in package.");
                            this.LogDebug($"Source info: {entry.size} bytes, {entry.date} timestamp");
                            this.LogDebug($"Target info: {fileInfo.Size} bytes, {fileInfo.LastWriteTimeUtc} timestamp");
                            collectedConfig.DriftedFiles = true;
                            return collectedConfig;
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
                            collectedConfig.DriftedFiles = true;
                            return collectedConfig;
                        }
                    }
                }

                this.LogInformation($"All package files and directories are present in {this.Template.TargetDirectory}.");
                collectedConfig.DriftedFiles = false;
                return collectedConfig;
            }
            catch (ProGetException ex)
            {
                this.LogError(ex.FullMessage);
                return null;
            }
        }

        public override Task<ComparisonResult> CompareAsync(PersistedConfiguration other, IOperationCollectionContext context)
        {
            var diffs = new List<Difference>();
            var collectedConfig = (ProGetPackageConfiguration)other ?? new ProGetPackageConfiguration { Exists = false };

            ComparisonResult compare()
            {
                if (this.Template.LocalRegistry != "None" && this.Template.LocalRegistry != collectedConfig.LocalRegistry)
                    diffs.Add(new Difference(nameof(this.Template.LocalRegistry), this.Template.LocalRegistry, collectedConfig.LocalRegistry));

                if (collectedConfig.Exists != this.Template.Exists)
                    diffs.Add(new Difference(nameof(this.Template.Exists), this.Template.Exists, collectedConfig.Exists));

                if (collectedConfig.PackageVersion != this.Template.PackageVersion)
                    diffs.Add(new Difference(nameof(this.Template.PackageVersion), this.Template.PackageVersion, collectedConfig.PackageVersion));

                if (collectedConfig.DriftedFiles)
                    diffs.Add(new Difference(nameof(this.Template.DriftedFiles), this.Template.DriftedFiles, collectedConfig.DriftedFiles));

                return new ComparisonResult(diffs);
            };
            return Task.FromResult(compare());
        }

        public override Task ConfigureAsync(IOperationExecutionContext context) => this.DeployAsynch(context);
        
        public async Task DownloadThenUpload(IOperationExecutionContext context)
        {
            var client = ProGetFeedClient.TryCreate(this.Template, context as ICredentialResolutionContext, this, context.CancellationToken);

            var packageVersion = await client.FindPackageVersionAsync(this.Template);
            if (packageVersion == null)
            {
                this.LogError($"Package {this.Template.PackageName} v{this.Template.PackageVersion} was not found.");
                return;
            }

            long size = packageVersion.Size == 0 ? 100 * 1024 * 1024 : packageVersion.Size;

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
            var targetDir = context.ResolvePath(this.Template.TargetDirectory);
                
            this.LogDebug($"Ensuring target directory ({targetDir}) exists...");
            await fileOps.CreateDirectoryAsync(targetDir);

            this.SetProgress(0, "downloading package");
            this.LogInformation("Downloading package...");
            var tempStream = TemporaryStream.Create(size);
            var sourceStream = await client.GetPackageStreamAsync(this.Template);
            await sourceStream.CopyToAsync(tempStream, 80 * 1024, context.CancellationToken, position => this.SetProgress((int)(100 * position / size), "downloading package"));

            var tempDirectoryName = fileOps.CombinePath(await fileOps.GetBaseWorkingDirectoryAsync().ConfigureAwait(false), Guid.NewGuid().ToString("N"));
            await fileOps.CreateDirectoryAsync(tempDirectoryName);
            var tempZipFileName = tempDirectoryName + ".zip";

            this.LogDebug($"Uploading package as temp file ({tempZipFileName }) on remote server");
            this.SetProgress(0, "copying package to agent");
            using (var remote = await fileOps.OpenFileAsync(tempZipFileName, FileMode.CreateNew, FileAccess.Write))
            {
                await tempStream.CopyToAsync(remote, 81920, context.CancellationToken, position => this.SetProgress((int)(100 * position / size), "copying package to agent"));
            }

            await this.InstallPackage(tempZipFileName, targetDir, context.CancellationToken);
        }
        private async Task InstallPackage(string tempZipFileName, string targetDir, CancellationToken cancellationToken)
        {
            this.LogInformation($"Installing package to {targetDir}...");
            using (var package = new UniversalPackage(tempZipFileName))
            {
                await package.ExtractContentItemsAsync(targetDir, cancellationToken);
            }
            this.LogInformation("Package installed.");
        }
        public async Task InstallLocal(IOperationExecutionContext context)
        {
            var client = ProGetFeedClient.TryCreate(this.Template, context as ICredentialResolutionContext, this, context.CancellationToken);
            
            var packageVersion = await client.FindPackageVersionAsync(this.Template);
            if (packageVersion == null)
            {
                this.LogError($"Package {this.Template.PackageName} v{this.Template.PackageVersion} was not found.");
                return;
            }

            long size = packageVersion.Size == 0 ? 100 * 1024 * 1024 : packageVersion.Size;

            var targetDir = context.ResolvePath(this.Template.TargetDirectory);
            this.LogDebug($"Ensuring target directory ({targetDir}) exists...");
            DirectoryEx.Create(targetDir);

            this.LogInformation("Downloading package...");
            var tempStream = TemporaryStream.Create(size);
            var sourceStream = await client.GetPackageStreamAsync(this.Template);
            await sourceStream.CopyToAsync(tempStream, 80 * 1024, context.CancellationToken); ;

            tempStream.Position = 0;

            this.LogInformation($"Installing package to {targetDir}...");
            using var package = new UniversalPackage(tempStream, true);
            await package.ExtractContentItemsAsync(targetDir, context.CancellationToken);

            this.LogInformation("Package installed.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure Universal Package ",
                    new Hilite(config[nameof(ProGetPackageConfiguration.PackageName)]),
                    $" ({config[nameof(ProGetPackageConfiguration.PackageVersion)]})."                    
                ),
                new RichDescription(
                    "are present in ",
                    new DirectoryHilite(config[nameof(ProGetPackageConfiguration.TargetDirectory)])
                )
            );
        }
    }
}
