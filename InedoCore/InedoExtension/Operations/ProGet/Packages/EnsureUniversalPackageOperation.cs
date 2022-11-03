using Inedo.Agents;
using Inedo.Extensibility.Configurations;
using Inedo.Extensions.Configurations.ProGet;
using Inedo.Extensions.UniversalPackages;

namespace Inedo.Extensions.Operations.ProGet.Packages
{
    [DisplayName("Ensure Universal Package Installed")]
    [Description("Ensures that the specified universal package is installed in the specified directory.")]
    [ScriptAlias("Ensure-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("proget")]
    [Note("To determine if a package is installed, the local package registry and package files are both checked. You can control these with LocalRegistry and FileCompare options.")]
    [Example(@"#Ensure that FooBarApp is Installed
ProGet::Ensure-Package
(
    From: MyPackageSource,
    Name: FooBarApp,
    Version: $FooBarVersion,
    To: D:\WebApps\FooBar.App,
    Ignore: web.config
);"
    )]
    public sealed class EnsureUniversalPackageOperation : EnsureOperation<ProGetPackageConfiguration>
    {
        private volatile OperationProgress progress = null;
        public override OperationProgress GetProgress() => this.progress;
        private void SetProgress(OperationProgress p) => this.progress = p;

        private bool ValidateConfiguration()
        {
            if (this.Template.Includes?.Any() == true
                || this.Template.Excludes?.Any() == true
                || this.Template.DeleteExtra == true)
            {
                this.LogError($"Includes/Excludes and DeleteExtra options are no longer supported.");
                return false;

            }

            if (this.Template.FileCompare == FileCompareOptions.DoNotCompare && this.Template.LocalRegistry == LocalRegistryOptions.None)
            {
                this.LogError(
                    $"{nameof(this.Template.FileCompare)} is set to {nameof(FileCompareOptions.DoNotCompare)} and " +
                    $"{nameof(this.Template.LocalRegistry)} is set to {nameof(LocalRegistryOptions.None)}, which means there's nothing to compare. ");
                return false;
            }
            return true;
        }

        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            if (!this.ValidateConfiguration())
                return null;

            var client = this.Template.TryCreateProGetFeedClient(context);
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
            if (this.Template.LocalRegistry != LocalRegistryOptions.None)
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
                    this.LogInformation($"Package not installed in local {this.Template.LocalRegistry} registery.");
                    collectedConfig.Exists = false;
                    return collectedConfig;
                }
                if (!string.Equals(matchedInstalledPackage.Version, package.Version.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    collectedConfig.PackageVersion = matchedInstalledPackage.Version;
                    return collectedConfig;
                }

                this.LogInformation($"Package installed in local {this.Template.LocalRegistry} registry.");
                if (!this.Template.Exists)
                {
                    collectedConfig.TargetDirectory = matchedInstalledPackage.InstallPath;
                    return collectedConfig;
                }
            }

            // Check the files
            if (this.Template.FileCompare != FileCompareOptions.DoNotCompare)
            {
                collectedConfig.FileCompare = this.Template.FileCompare;

                var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

                if (!await fileOps.DirectoryExistsAsync(this.Template.TargetDirectory).ConfigureAwait(false))
                {
                    this.LogInformation(this.Template.TargetDirectory + " does not exist.");
                    collectedConfig.Exists = false;
                    return collectedConfig;
                }
                collectedConfig.TargetDirectory = this.Template.TargetDirectory;

                var mask = new MaskingContext(new[] { "**" }, this.Template.IgnoreFiles);

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

                if (versionInfo.FileList == null)
                {
                    this.LogError("File list is unavailable for this package; it may be an orphaned entry.");
                    return null;
                }

                this.LogDebug($"Package contains {versionInfo.FileList.Length} file system entries.");

                foreach (var entry in versionInfo.FileList)
                {
                    var relativeName = entry.Name;
                    if (!mask.IsMatch(relativeName))
                        continue;

                    var file = remoteFiles.GetValueOrDefault(relativeName);
                    if (file == null)
                    {
                        this.LogInformation($"Entry {relativeName} is not present in {this.Template.TargetDirectory}.");
                        collectedConfig.DriftedFileNames.Add(relativeName);
                        continue;
                    }

                    if (!entry.Name.EndsWith("/"))
                    {
                        var fileInfo = (SlimFileInfo)file;
                        if (entry.Size != fileInfo.Size
                                || this.Template.FileCompare == FileCompareOptions.FileSizeAndLastModified && entry.Date != fileInfo.LastWriteTimeUtc)
                        {
                            this.LogInformation($"File {relativeName} in {this.Template.TargetDirectory} is different from file in package.");
                            this.LogDebug($"Source info: {entry.Size} bytes, {entry.Date} timestamp");
                            this.LogDebug($"Target info: {fileInfo.Size} bytes, {fileInfo.LastWriteTimeUtc} timestamp");
                            collectedConfig.DriftedFileNames.Add(relativeName);
                        }
                    }
                }
                if (collectedConfig.DriftedFileNames.Count > 0)
                    return collectedConfig;

                this.LogInformation($"All package files and directories are present in {this.Template.TargetDirectory}.");
                collectedConfig.DriftedFiles = false;

            }

            return collectedConfig;
        }

        public override Task<ComparisonResult> CompareAsync(PersistedConfiguration other, IOperationCollectionContext context)
        {
            var diffs = new List<Difference>();
            var collectedConfig = (ProGetPackageConfiguration)other ?? new ProGetPackageConfiguration { Exists = false };

            ComparisonResult compare()
            {
                if (this.Template.LocalRegistry != LocalRegistryOptions.None && this.Template.LocalRegistry != collectedConfig.LocalRegistry)
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

        public override Task ConfigureAsync(IOperationExecutionContext context)
        {
            if (!this.ValidateConfiguration())
                return InedoLib.NullTask;

            return this.Template.InstallPackageAsync(context, this.SetProgress);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure Universal Package ",
                    new Hilite(config[nameof(IFeedPackageInstallationConfiguration.PackageName)]),
                    $" ({AH.CoalesceString(config[nameof(IFeedPackageInstallationConfiguration.PackageVersion)], "latest")})."
                ),
                new RichDescription(
                    "is installed in ",
                    new DirectoryHilite(config[nameof(IFeedPackageInstallationConfiguration.TargetDirectory)])
                )
            );
        }
    }
}
