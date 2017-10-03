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
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.RaftRepositories;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    internal static partial class PackageDeployer
    {
#if Hedgehog
        public static Task DeployAsync(IOperationExecutionContext context, IProGetPackageInstallTemplate template, ILogSink log, string installationReason, bool recordDeployment, Action<OperationProgress> setProgress = null)
            => DeployAsync(context, template, new ShimLogger(log), installationReason, recordDeployment, setProgress);
#endif
        public static async Task DeployAsync(IOperationExecutionContext context, IProGetPackageInstallTemplate template, ILogger log, string installationReason, bool recordDeployment, Action<OperationProgress> setProgress = null)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
            var client = new ProGetClient(template.FeedUrl, template.FeedName, template.UserName, template.Password, log, context.CancellationToken);

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
                using (var content = await client.DownloadPackageContentAsync(packageId, version, deployInfo, (position, length) => setProgress?.Invoke(new OperationProgress(length == 0 ? null : (int?)(100 * position / length), "downloading package"))).ConfigureAwait(false))
                {
                    var tempDirectoryName = fileOps.CombinePath(await fileOps.GetBaseWorkingDirectoryAsync().ConfigureAwait(false), Guid.NewGuid().ToString("N"));
                    var tempZipFileName = tempDirectoryName + ".zip";

                    try
                    {
                        setProgress?.Invoke(new OperationProgress(0, "copying package to agent"));
                        using (var remote = await fileOps.OpenFileAsync(tempZipFileName, FileMode.CreateNew, FileAccess.Write).ConfigureAwait(false))
                        {
                            await content.CopyToAsync(remote, 81920, context.CancellationToken, position => setProgress?.Invoke(new OperationProgress((int)(100 * position / content.Length), "copying package to agent"))).ConfigureAwait(false);
                        }
                        setProgress?.Invoke(new OperationProgress("extracting package to temporary directory"));
                        await fileOps.ExtractZipFileAsync(tempZipFileName, tempDirectoryName, true).ConfigureAwait(false);

                        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var expectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        content.Position = 0;
                        using (var zip = new ZipArchive(content, ZipArchiveMode.Read, true))
                        {
                            foreach (var entry in zip.Entries)
                            {
                                // TODO: use AH.ReadZip when it is available in Otter.
                                var fullName = entry.FullName.Replace('\\', '/');
                                if (!fullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase) || fullName.Length <= "package/".Length)
                                    continue;

                                if (entry.IsDirectory())
                                {
                                    expectedDirectories.Add(fullName.Substring("package/".Length).Trim('/'));
                                }
                                else
                                {
                                    expectedFiles.Add(fullName.Substring("package/".Length));
                                    var parts = fullName.Substring("package/".Length).Split('/');
                                    for (int i = 1; i < parts.Length; i++)
                                    {
                                        // Add directories that are not explicitly in the zip file.
                                        expectedDirectories.Add(string.Join("/", parts.Take(i)));
                                    }
                                }
                            }
                        }

                        setProgress?.Invoke(new OperationProgress("ensuring target directory exists"));
                        await fileOps.CreateDirectoryAsync(template.TargetDirectory).ConfigureAwait(false);

                        int index = 0;
                        if (template.DeleteExtra)
                        {
                            setProgress?.Invoke(new OperationProgress("checking existing files"));
                            var remoteFileList = await fileOps.GetFileSystemInfosAsync(template.TargetDirectory, MaskingContext.IncludeAll).ConfigureAwait(false);

                            foreach (var file in remoteFileList)
                            {
                                index++;
                                setProgress?.Invoke(new OperationProgress(100 * index / remoteFileList.Count, "checking existing files"));

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

                        index = 0;
                        foreach (var relativeName in expectedDirectories)
                        {
                            index++;
                            setProgress?.Invoke(new OperationProgress(100 * index / expectedDirectories.Count, "ensuring target subdirectories exist"));

                            await fileOps.CreateDirectoryAsync(fileOps.CombinePath(template.TargetDirectory, relativeName)).ConfigureAwait(false);
                        }

                        index = 0;
                        foreach (var relativeName in expectedFiles)
                        {
                            var sourcePath = fileOps.CombinePath(tempDirectoryName, "package", relativeName);
                            var targetPath = fileOps.CombinePath(template.TargetDirectory, relativeName);

                            index++;
                            setProgress?.Invoke(new OperationProgress(100 * index / expectedFiles.Count, "moving files to target directory"));

                            await fileOps.MoveFileAsync(sourcePath, targetPath, true).ConfigureAwait(false);
                        }

                        setProgress?.Invoke(new OperationProgress("cleaning temporary files"));
                    }
                    finally
                    {
                        await Task.WhenAll(
                            fileOps.DeleteFileAsync(tempZipFileName),
                            fileOps.DeleteDirectoryAsync(tempDirectoryName)
                        ).ConfigureAwait(false);
                    }
                }

                setProgress?.Invoke(new OperationProgress("recording server package information"));
                await RecordServerPackageInfoAsync(context, packageId.ToString(), version, client.GetViewPackageUrl(packageId, version), log).ConfigureAwait(false);

                setProgress?.Invoke(new OperationProgress("recording package installation in machine registry"));
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

                    try
                    {
                        using (var cancellationTokenSource = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10)))
                        using (context.CancellationToken.Register(() => cancellationTokenSource.Cancel()))
                        {
                            await registry.LockAsync(cancellationTokenSource.Token).ConfigureAwait(false);

                            await registry.RegisterPackageAsync(package, context.CancellationToken).ConfigureAwait(false);

                            // doesn't need to be in a finally because dispose will unlock if necessary, but prefer doing it asynchronously
                            await registry.UnlockAsync().ConfigureAwait(false);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        log.LogWarning("Registering the package in the machine package registry timed out.");
                    }
                }
            }
            catch (ProGetException ex)
            {
                log.LogError(ex.FullMessage);
                return;
            }

            setProgress?.Invoke(null);
            log.LogInformation("Package deployed!");
        }
    }
}
