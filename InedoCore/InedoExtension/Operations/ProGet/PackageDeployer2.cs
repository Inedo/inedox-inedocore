using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.UniversalPackages;
using Inedo.IO;

namespace Inedo.Extensions.Operations.ProGet
{
    internal static class PackageDeployer2
    {
        private static SemaphoreSlim registryLock => PackageDeployer.registryLock;

        public static async Task DeployAsynch(this EnsurePackageOperation operation, IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
            var client = ProGetFeedClient.TryCreate(operation.Template, context as ICredentialResolutionContext ?? CredentialResolutionContext.None, operation, context.CancellationToken);

            Action<OperationProgress> setProgress = operation.SetProgress;
            var template = operation.Template;

            try
            {
                var packageToInstall = await client.FindPackageVersionAsync(template.PackageName, template.PackageVersion);
                if (packageToInstall == null)
                {
                    operation.LogError($"Package {template.PackageName} ({template.PackageVersion}) not found.");
                    return;
                }

                var operationName = "Ensure-Package";
                var jobExec = await context.Agent.TryGetServiceAsync<IRemoteJobExecuter>().ConfigureAwait(false);
                var deployInfo = PackageDeploymentData.Create(context, operation, $"Deployed by {operationName} operation. See the URL for more info.");
                var targetRootPath = context.ResolvePath(template.TargetDirectory);
                operation.LogDebug("Target path: " + targetRootPath);

                operation.LogInformation("Downloading package...");
                if (jobExec != null && template.DirectDownload)
                {
                    operation.LogWarning("DirectDownload is not implemented.");

                }
                
                //else
                {

                    using var content = await client.DownloadPackageContentAsync(
                        packageToInstall.FullName, packageToInstall.Version, deployInfo,
                        (position, length) => setProgress?.Invoke(new OperationProgress(length == 0 ? null : (int?)(100 * position / length), "downloading package")));
                    var tempDirectoryName = fileOps.CombinePath(await fileOps.GetBaseWorkingDirectoryAsync().ConfigureAwait(false), Guid.NewGuid().ToString("N"));

                    // ensure directory exists on server
                    await fileOps.CreateDirectoryAsync(tempDirectoryName);
                    var tempZipFileName = tempDirectoryName + ".zip";

                    try
                    {
                        setProgress?.Invoke(new OperationProgress(0, "copying package to agent"));
                        using (var remote = await fileOps.OpenFileAsync(tempZipFileName, FileMode.CreateNew, FileAccess.Write).ConfigureAwait(false))
                        {
                            await content.CopyToAsync(remote, 81920, context.CancellationToken, position => setProgress?.Invoke(new OperationProgress((int)(100 * position / content.Length), "copying package to agent"))).ConfigureAwait(false);
                        }
                        setProgress?.Invoke(new OperationProgress("extracting package to temporary directory"));
                        await fileOps.ExtractZipFileAsync(tempZipFileName, tempDirectoryName, IO.FileCreationOptions.Overwrite).ConfigureAwait(false);

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

                        if (jobExec != null)
                        {
                            var job = new PackageDeployer.PackageDeploymentJob
                            {
                                DeleteExtra = template.DeleteExtra,
                                TargetRootPath = targetRootPath,
                                TempDirectoryName = tempDirectoryName,
                                ExpectedDirectories = expectedDirectories.ToArray(),
                                ExpectedFiles = expectedFiles.ToArray()
                            };

                            job.MessageLogged += (s, e) => operation.Log(e.Level, e.Message);
                            job.ProgressChanged += (s, e) => setProgress?.Invoke(e);

                            setProgress?.Invoke(new OperationProgress("starting remote job on agent"));
                            await jobExec.ExecuteJobAsync(job, context.CancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            setProgress?.Invoke(new OperationProgress("ensuring target directory exists"));
                            await fileOps.CreateDirectoryAsync(targetRootPath).ConfigureAwait(false);

                            int index = 0;
                            if (template.DeleteExtra)
                            {
                                setProgress?.Invoke(new OperationProgress("checking existing files"));
                                var remoteFileList = await fileOps.GetFileSystemInfosAsync(targetRootPath, MaskingContext.IncludeAll).ConfigureAwait(false);

                                foreach (var file in remoteFileList)
                                {
                                    index++;
                                    setProgress?.Invoke(new OperationProgress(100 * index / remoteFileList.Count, "checking existing files"));

                                    var relativeName = file.FullName.Substring(targetRootPath.Length).Replace('\\', '/').Trim('/');
                                    if (file is SlimDirectoryInfo)
                                    {
                                        if (!expectedDirectories.Contains(relativeName))
                                        {
                                            operation.LogDebug("Deleting extra directory: " + relativeName);
                                            await fileOps.DeleteDirectoryAsync(file.FullName).ConfigureAwait(false);
                                        }
                                    }
                                    else
                                    {
                                        if (!expectedFiles.Contains(relativeName))
                                        {
                                            operation.LogDebug($"Deleting extra file: " + relativeName);
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

                                await fileOps.CreateDirectoryAsync(fileOps.CombinePath(targetRootPath, relativeName)).ConfigureAwait(false);
                            }

                            index = 0;
                            foreach (var relativeName in expectedFiles)
                            {
                                var sourcePath = fileOps.CombinePath(tempDirectoryName, "package", relativeName);
                                var targetPath = fileOps.CombinePath(targetRootPath, relativeName);

                                index++;
                                setProgress?.Invoke(new OperationProgress(100 * index / expectedFiles.Count, "moving files to target directory"));

                                await fileOps.MoveFileAsync(sourcePath, targetPath, true).ConfigureAwait(false);
                            }
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
                setProgress?.Invoke(new OperationProgress("recording package installation in machine registry"));
                using var registry = await RemotePackageRegistry.GetRegistryAsync(context.Agent, false).ConfigureAwait(false);
                var package = new RegisteredPackage
                {
                    Group = packageToInstall.Group,
                    Name = packageToInstall.Name,
                    Version = packageToInstall.Version.ToString(),
                    InstallPath = targetRootPath,
                    FeedUrl = template.FeedUrl,
                    InstallationDate = DateTimeOffset.Now.ToString("o"),
                    InstallationReason = operationName,
                    InstalledUsing = $"{SDK.ProductName}/{SDK.ProductVersion} (InedoCore/{Extension.Version})"
                };

                await registryLock.WaitAsync(context.CancellationToken).ConfigureAwait(false);
                try
                {
                    using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
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
                    operation.LogWarning("Registering the package in the machine package registry timed out.");
                }
                finally
                {
                    registryLock.Release();
                }
            }
            catch (ProGetException ex)
            {
                operation.LogError(ex.FullMessage);
                return;
            }

            operation.SetProgress(null);
            operation.LogInformation("Package deployed!");
        }
    }
}
