﻿using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.IO;
using Inedo.Serialization;
using Inedo.UPack;
using Inedo.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Get Universal Package (Deprecated)")]
    [Description("Downloads the contents of a Universal package to a specified directory.")]
    [ScriptAlias("Get-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Note("This has been deprecated in favor of Ensure-Package and Install-Package")]
    [Undisclosed]
    [Tag("proget")]
    public sealed partial class GetPackageOperation : ExecuteOperation, IFeedPackageConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public string PackageSourceName { get; set; }

        [Required]
        [ScriptAlias("Feed")]
        [DisplayName("Feed name")]
        [SuggestableValue(typeof(FeedNameSuggestionProvider))]
        public string FeedName { get; set; }

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }

        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("latest")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Required]
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory path on disk of the package contents.")]
        public string TargetDirectory { get; set; }

        [ScriptAlias("DeleteExtra")]
        [DisplayName("Delete files not in Package")]
        public bool DeleteExtra { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Server")]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from credential")]
        public string FeedUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access the specified feed.")]
        [PlaceholderText("Use user name from credential")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [Description("The password of a user in ProGet that can access the specified feed.")]
        [PlaceholderText("Use password from credential")]
        public string Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiKey")]
        [DisplayName("ProGet API Key")]
        [PlaceholderText("Use API Key from package source")]
        [Description("An API Key that can access this feed.")]
        public string ApiKey { get; set; }

        [Category("Advanced")]
        [ScriptAlias("RecordDeployment")]
        [DisplayName("Record deployment in ProGet")]
        [DefaultValue(true)]
        public bool RecordDeployment { get; set; } = true;

        private volatile OperationProgress progress = null;
        public override OperationProgress GetProgress() => progress;

        public override Task ExecuteAsync(IOperationExecutionContext context) => PackageDeployer.DeployAsync(context, this, this, "Get-Package", this.RecordDeployment, p => this.progress = p);

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            object[] versionText;
            if (string.IsNullOrWhiteSpace(config[nameof(this.PackageVersion)]))
                versionText = new object[] { new Hilite("latest version") };
            else
                versionText = new object[] { "version ", new Hilite(config[nameof(this.PackageVersion)]) };

            return new ExtendedRichDescription(
                new RichDescription(
                    "Install universal package contents of ",
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

        private static class PackageDeployer
        {
            internal static readonly SemaphoreSlim registryLock = new SemaphoreSlim(1, 1);



            public static async Task DeployAsync(IOperationExecutionContext context, GetPackageOperation template, ILogSink log, string installationReason, bool recordDeployment, Action<OperationProgress> setProgress = null)
            {
                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
                var client = new ObsoleteProGetClient(template.FeedUrl, template.FeedName, template.UserName, template.Password, log, context.CancellationToken);

                try
                {
                    var packageId = Inedo.Extensions.Operations.ProGet.ObsoleteProGetClient.PackageName.Parse(template.PackageName);

                    log.LogInformation($"Connecting to {template.FeedUrl} to get metadata for {template.PackageName}...");
                    var packageInfo = await client.GetPackageInfoAsync(packageId).ConfigureAwait(false);

                    string version;

                    if (string.Equals(template.PackageVersion, "latest-stable", StringComparison.OrdinalIgnoreCase))
                    {
                        var stableVersions = packageInfo.versions
                            .Select(v => UniversalPackageVersion.TryParse(v))
                            .Where(v => string.IsNullOrEmpty(v?.Prerelease));
                        if (!stableVersions.Any())
                        {
                            log.LogError($"Package {template.PackageName} does not have any stable versions.");
                            return;
                        }

                        version = stableVersions.Max().ToString();
                        log.LogInformation($"Latest stable version of {template.PackageName} is {version}.");
                    }
                    else if (!string.IsNullOrEmpty(template.PackageVersion) && !string.Equals(template.PackageVersion, "latest", StringComparison.OrdinalIgnoreCase))
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

                    var deployInfo = recordDeployment ? ObsoleteProGetClient.PackageDeploymentData.Create(context, log, $"Deployed by {installationReason} operation. See the URL for more info.") : null;
                    var targetRootPath = context.ResolvePath(template.TargetDirectory);
                    log.LogDebug("Target path: " + targetRootPath);

                    log.LogInformation("Downloading package...");
                    using (var content = await client.DownloadPackageContentAsync(packageId, version, deployInfo, (position, length) => setProgress?.Invoke(new OperationProgress(length == 0 ? null : (int?)(100 * position / length), "downloading package"))).ConfigureAwait(false))
                    {
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

                            var jobExec = await context.Agent.TryGetServiceAsync<IRemoteJobExecuter>().ConfigureAwait(false);
                            if (jobExec != null)
                            {
                                var job = new PackageDeploymentJob
                                {
                                    DeleteExtra = template.DeleteExtra,
                                    TargetRootPath = targetRootPath,
                                    TempDirectoryName = tempDirectoryName,
                                    ExpectedDirectories = expectedDirectories.ToArray(),
                                    ExpectedFiles = expectedFiles.ToArray()
                                };

                                job.MessageLogged += (s, e) => log.Log(e.Level, e.Message);
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
                    using (var registry = await RemotePackageRegistry.GetRegistryAsync(context.Agent, false).ConfigureAwait(false))
                    {
                        var package = new RegisteredPackageModel
                        {
                            Group = packageId.Group,
                            Name = packageId.Name,
                            Version = version,
                            InstallPath = targetRootPath,
                            FeedUrl = template.FeedUrl,
                            InstallationDate = DateTimeOffset.Now.ToString("o"),
                            InstallationReason = installationReason,
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
                            log.LogWarning("Registering the package in the machine package registry timed out.");
                        }
                        finally
                        {
                            registryLock.Release();
                        }
                    }
                }
                catch (ObsoleteProGetClient.ProGetException ex)
                {
                    log.LogError(ex.FullMessage);
                    return;
                }

                setProgress?.Invoke(null);
                log.LogInformation("Package deployed!");
            }

            internal sealed class PackageDeploymentJob : RemoteJob
            {
                public bool DeleteExtra { get; set; }
                public string TempDirectoryName { get; set; }
                public string TargetRootPath { get; set; }
                public string[] ExpectedDirectories { get; set; }
                public string[] ExpectedFiles { get; set; }

                public override Task<object> ExecuteAsync(CancellationToken cancellationToken)
                {
                    this.SetProgress(cancellationToken, "ensuring target directory exists");
                    DirectoryEx.Create(this.TargetRootPath);

                    int index = 0;
                    if (this.DeleteExtra)
                    {
                        this.SetProgress(cancellationToken, "checking existing files");
                        var remoteFileList = DirectoryEx.GetFileSystemInfos(this.TargetRootPath, MaskingContext.IncludeAll);

                        foreach (var file in remoteFileList)
                        {
                            index++;
                            this.SetProgress(cancellationToken, "checking existing files", 100 * index / remoteFileList.Count);

                            var relativeName = file.FullName.Substring(this.TargetRootPath.Length).Replace('\\', '/').Trim('/');
                            if (file is SlimDirectoryInfo)
                            {
                                if (!this.ExpectedDirectories.Contains(relativeName))
                                {
                                    this.LogDebug("Deleting extra directory: " + relativeName);
                                    DirectoryEx.Delete(file.FullName);
                                }
                            }
                            else
                            {
                                if (!this.ExpectedFiles.Contains(relativeName))
                                {
                                    this.LogDebug($"Deleting extra file: " + relativeName);
                                    FileEx.Delete(file.FullName);
                                }
                            }
                        }
                    }

                    index = 0;
                    foreach (var relativeName in this.ExpectedDirectories)
                    {
                        index++;
                        this.SetProgress(cancellationToken, "ensuring target subdirectories exist", 100 * index / this.ExpectedDirectories.Length);

                        DirectoryEx.Create(PathEx.Combine(Path.DirectorySeparatorChar, this.TargetRootPath, relativeName));
                    }

                    index = 0;
                    foreach (var relativeName in this.ExpectedFiles)
                    {
                        var sourcePath = PathEx.Combine(PathEx.Combine(Path.DirectorySeparatorChar, this.TempDirectoryName, "package"), relativeName);
                        var targetPath = PathEx.Combine(Path.DirectorySeparatorChar, this.TargetRootPath, relativeName);

                        index++;
                        this.SetProgress(cancellationToken, "moving files to target directory", 100 * index / this.ExpectedFiles.Length);

                        FileEx.Move(sourcePath, targetPath, true);
                    }

                    return InedoLib.NullTask;
                }

                public event EventHandler<OperationProgress> ProgressChanged;

                private void SetProgress(CancellationToken cancellationToken, string message, int? percent = null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (var stream = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding, true))
                        {
                            writer.Write(percent ?? -1);
                            writer.Write(message ?? string.Empty);
                        }

                        this.Post(stream.ToArray());
                    }
                }

                protected override void DataReceived(byte[] data)
                {
                    using (var stream = new MemoryStream(data, false))
                    using (var reader = new BinaryReader(stream, InedoLib.UTF8Encoding))
                    {
                        int percent = reader.ReadInt32();
                        var message = reader.ReadString();
                        this.ProgressChanged?.Invoke(this, new OperationProgress(AH.NullIf(percent, -1), message));
                    }
                }

                public override void Serialize(Stream stream)
                {
                    SlimBinaryFormatter.Serialize(this.DeleteExtra, stream);
                    SlimBinaryFormatter.Serialize(this.TempDirectoryName, stream);
                    SlimBinaryFormatter.Serialize(this.TargetRootPath, stream);
                    SlimBinaryFormatter.Serialize(this.ExpectedDirectories, stream);
                    SlimBinaryFormatter.Serialize(this.ExpectedFiles, stream);
                }

                public override void Deserialize(Stream stream)
                {
                    this.DeleteExtra = (bool)SlimBinaryFormatter.Deserialize(stream);
                    this.TempDirectoryName = (string)SlimBinaryFormatter.Deserialize(stream);
                    this.TargetRootPath = (string)SlimBinaryFormatter.Deserialize(stream);
                    this.ExpectedDirectories = (string[])SlimBinaryFormatter.Deserialize(stream);
                    this.ExpectedFiles = (string[])SlimBinaryFormatter.Deserialize(stream);
                }

                public override void SerializeResponse(Stream stream, object result)
                {
                    // do nothing
                }

                public override object DeserializeResponse(Stream stream)
                {
                    return null;
                }
            }
        }
    }
}
