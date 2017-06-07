using System;
using System.Collections.Generic;
using System.IO;
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
        public static async Task DeployAsync(IOperationExecutionContext context, IProGetPackageInstallTemplate template, ILogger log, string installationReason)
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

                var deployInfo = PackageDeploymentData.Create(context, log, "Deployed by Ensure-Package operation. See the URL for more info.");
                var targetRootPath = context.ResolvePath(template.TargetDirectory);

                log.LogInformation("Downloading package...");
                using (var zip = await client.DownloadPackageAsync(packageId, version, deployInfo).ConfigureAwait(false))
                {
                    var dirsCreated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    await fileOps.CreateDirectoryAsync(targetRootPath).ConfigureAwait(false);
                    dirsCreated.Add(targetRootPath);

                    if (template.DeleteExtra)
                    {
                        var remoteFileList = await fileOps.GetFileSystemInfosAsync(targetRootPath, MaskingContext.IncludeAll).ConfigureAwait(false);

                        foreach (var file in remoteFileList)
                        {
                            var relativeName = file.FullName.Substring(targetRootPath.Length).Replace('\\', '/').Trim('/');
                            var entry = zip.GetEntry("package/" + relativeName);
                            if (file is SlimDirectoryInfo)
                            {
                                if (entry == null || !entry.IsDirectory())
                                {
                                    log.LogDebug("Deleting extra directory: " + relativeName);
                                    await fileOps.DeleteDirectoryAsync(file.FullName).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                if (entry == null || entry.IsDirectory())
                                {
                                    log.LogDebug($"Deleting extra file: " + relativeName);
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

                        var targetPath = fileOps.CombinePath(targetRootPath, relativeName);
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
