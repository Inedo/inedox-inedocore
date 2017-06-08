using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Extensions.UniversalPackages;
using Inedo.IO;
#if Otter
using Inedo.Otter.Extensibility.Operations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    internal static partial class PackageDeployer
    {
        public static async Task DeployAsync(IOperationExecutionContext context, IProGetPackageInstallTemplate template, ILogger log, string installationReason, bool recordDeployment)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
            var client = new ProGetClient(template.FeedUrl, template.FeedName, template.UserName, template.Password, log);

            try
            {
                var packageId = PackageName.Parse(template.PackageName);

                log.LogInformation($"Connecting to {template.FeedUrl} to get metadata for {template.PackageName}...");
                var packageInfo = await client.GetPackageInfoAsync(packageId).ConfigureAwait(false);

                string version;

                if (!string.IsNullOrEmpty(template.PackageVersion) && !string.Equals(template.PackageVersion, "latest", StringComparison.OrdinalIgnoreCase))
                {
                    if (!packageInfo.versions.Contains(template.PackageVersion, StringComparer.OrdinalIgnoreCase))
                    {
                        log.LogError($"Package {template.PackageName} does not have a version {template.PackageVersion}.");
                        return;
                    }

                    version = template.PackageVersion;
                }
                else
                {
                    version = packageInfo.latestVersion;
                    log.LogInformation($"Latest version of {template.PackageName} is {version}.");
                }

                var deployInfo = recordDeployment ? PackageDeploymentData.Create(context, log, $"Deployed by {installationReason} operation. See the URL for more info.") : null;
                var targetRootPath = context.ResolvePath(template.TargetDirectory);

                log.LogInformation("Downloading package...");
                using (var content = await client.DownloadPackageContentAsync(packageId, version, deployInfo).ConfigureAwait(false))
                {
                    var tempDirectoryName = fileOps.CombinePath(await fileOps.GetBaseWorkingDirectoryAsync().ConfigureAwait(false), Guid.NewGuid().ToString("N"));
                    var tempZipFileName = tempDirectoryName + ".zip";

                    try
                    {
                        using (var remote = await fileOps.OpenFileAsync(tempZipFileName, FileMode.CreateNew, FileAccess.Write).ConfigureAwait(false))
                        {
                            await content.CopyToAsync(remote).ConfigureAwait(false);
                        }
                        await fileOps.ExtractZipFileAsync(tempZipFileName, tempDirectoryName, true).ConfigureAwait(false);

                        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var expectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        content.Position = 0;
                        using (var zip = new ZipArchive(content, ZipArchiveMode.Read, true))
                        {
                            foreach (var entry in zip.Entries)
                            {
                                if (!entry.FullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase) || entry.FullName.Length <= "package/".Length)
                                    continue;

                                if (entry.IsDirectory())
                                    expectedDirectories.Add(entry.FullName.Substring("package/".Length));
                                else
                                    expectedFiles.Add(entry.FullName.Substring("package/".Length));
                            }
                        }

                        await fileOps.CreateDirectoryAsync(template.TargetDirectory).ConfigureAwait(false);

                        if (template.DeleteExtra)
                        {
                            var remoteFileList = await fileOps.GetFileSystemInfosAsync(template.TargetDirectory, MaskingContext.IncludeAll).ConfigureAwait(false);

                            foreach (var file in remoteFileList)
                            {
                                var relativeName = file.FullName.Substring(template.TargetDirectory.Length).Replace('\\', '/').Trim('/');
                                if (file is SlimDirectoryInfo)
                                {
                                    if (!expectedDirectories.Contains(relativeName))
                                    {
                                        log.LogDebug("Deleting extra directory: " + relativeName);
                                        await fileOps.DeleteDirectoryAsync(file.FullName).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    if (!expectedFiles.Contains(relativeName))
                                    {
                                        log.LogDebug($"Deleting extra file: " + relativeName);
                                        await fileOps.DeleteFileAsync(file.FullName).ConfigureAwait(false);
                                    }
                                }
                            }
                        }

                        foreach (var relativeName in expectedDirectories)
                        {
                            await fileOps.CreateDirectoryAsync(fileOps.CombinePath(template.TargetDirectory, relativeName)).ConfigureAwait(false);
                        }

                        foreach (var relativeName in expectedFiles)
                        {
                            var sourcePath = fileOps.CombinePath(tempDirectoryName, "package", relativeName);
                            var targetPath = fileOps.CombinePath(template.TargetDirectory, relativeName);

                            await fileOps.MoveFileAsync(sourcePath, targetPath, true).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        await Task.WhenAll(
                            fileOps.DeleteFileAsync(tempZipFileName),
                            fileOps.DeleteDirectoryAsync(tempDirectoryName)
                        ).ConfigureAwait(false);
                    }
                }

                await RecordServerPackageInfoAsync(context, packageId.ToString(), version, client.GetViewPackageUrl(packageId, version), log).ConfigureAwait(false);

                using (var registry = await PackageRegistry.GetRegistryAsync(context.Agent, false).ConfigureAwait(false))
                {
                    var package = new RegisteredPackage
                    {
                        Group = packageId.Group,
                        Name = packageId.Name,
                        Version = version,
                        InstallPath = targetRootPath,
                        FeedUrl = template.FeedUrl,
                        InstallationDate = DateTimeOffset.Now.ToString("o"),
                        InstallationReason = installationReason,
                        InstalledUsing = $"{Extension.Product}/{Extension.ProductVersion} (InedoCore/{Extension.Version})"
                    };

                    await registry.LockAsync(context.CancellationToken).ConfigureAwait(false);
                    await registry.RegisterPackageAsync(package, context.CancellationToken).ConfigureAwait(false);

                    // doesn't need to be in a finally because dispose will unlock if necessary, but prefer doing it asynchronously
                    await registry.UnlockAsync().ConfigureAwait(false);
                }
            }
            catch (ProGetException ex)
            {
                log.LogError(ex.FullMessage);
                return;
            }

            log.LogInformation("Package deployed!");
        }
    }
}
