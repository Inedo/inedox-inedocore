using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.UniversalPackages;
using Inedo.IO;
using Inedo.Serialization;
using Inedo.UPack.Packaging;
using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;


namespace Inedo.Extensions.Operations.ProGet
{
    internal static class Extensions
    {
        public static ProGetFeedClient TryCreateProGetFeedClient(this IFeedPackageConfiguration feedConfig, IOperationExecutionContext context)
            => TryCreateProGetFeedClient(feedConfig, context as ICredentialResolutionContext ?? CredentialResolutionContext.None, context.Log, context.CancellationToken);
        public static ProGetFeedClient TryCreateProGetFeedClient(this IFeedPackageConfiguration feedConfig, ICredentialResolutionContext context)
            => TryCreateProGetFeedClient(feedConfig, context, null, default);
        private static ProGetFeedClient TryCreateProGetFeedClient(IFeedPackageConfiguration feedConfig, ICredentialResolutionContext context, ILogSink log, CancellationToken cancellationToken)
        {
            if (feedConfig == null)
                return null;

            UniversalPackageSource packageSource = null;
            if (!string.IsNullOrEmpty(feedConfig.PackageSourceName))
            {
                packageSource = SecureResource.TryCreate(feedConfig.PackageSourceName, context) as UniversalPackageSource;
                if (packageSource == null)
                {
                    log.LogDebug($"No {nameof(UniversalPackageSource)} with the name {feedConfig.PackageSourceName} was found.");
                    return null;
                }
            }

            // endpoint can be specified via secure resource or directly
            string apiEndpointUrl = null;
            {
                if (!string.IsNullOrEmpty(feedConfig.FeedUrl))
                    apiEndpointUrl = feedConfig?.FeedUrl;

                else if (!string.IsNullOrEmpty(packageSource?.ApiEndpointUrl))
                    apiEndpointUrl = packageSource.ApiEndpointUrl;

                else
                {
                    log.LogDebug($"No Api Endpoint URL was specified.");
                    return null;
                }
            }

            // rebuild URL if FeedName is overriden
            if (!string.IsNullOrEmpty(feedConfig.FeedName))
            {
                var match = ProGetFeedClient.ApiEndPointUrlRegex.Match(apiEndpointUrl ?? "");
                if (!match.Success)
                {
                    log.LogDebug($"{apiEndpointUrl} is not a valid {nameof(UniversalPackageSource)} url.");
                    return null;
                }

                apiEndpointUrl = $"{match.Groups["baseUrl"]}/upack/{feedConfig.FeedName}/";
            }

            SecureCredentials credentials;
            if (!string.IsNullOrEmpty(feedConfig.ApiKey))
                credentials = new TokenCredentials { Token = AH.CreateSecureString(feedConfig.ApiKey) };
            else if (!string.IsNullOrEmpty(feedConfig.UserName))
                credentials = new UsernamePasswordCredentials { UserName = feedConfig.UserName, Password = AH.CreateSecureString(feedConfig.Password) };
            else
                credentials = packageSource?.GetCredentials(context);

            return new ProGetFeedClient(apiEndpointUrl, credentials, log, cancellationToken);
        }


        public static async Task ResolveAttachedPackageAsync(this IFeedPackageConfiguration config, IOperationExecutionContext context)
        {
            if (config.PackageVersion == "attached")
            {
                if (SDK.ProductName != "BuildMaster")
                    throw new ExecutionFailureException($"Setting \"attached\" for the package version is only supported in BuildMaster.");

                context.Log.LogDebug("Searching for attached package version...");

                var packageManager = await context.TryGetServiceAsync<IPackageManager>();
                var match = (await packageManager.GetBuildPackagesAsync(context.CancellationToken))
                    .FirstOrDefault(p => p.Active
                        && p.PackageType == AttachedPackageType.Universal
                        && string.Equals(p.Name, config.PackageName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.PackageSource, config.PackageSourceName, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                    throw new ExecutionFailureException($"The current build has no active attached packages named {config.PackageName} from source {config.PackageSourceName}.");

                context.Log.LogInformation($"Package version from attached package {config.PackageName} (source {config.PackageSourceName}): {match.Version}");
                config.PackageVersion = match.Version;
            }
        }
        public static void PrepareCredentialPropertiesForRemote(this IFeedPackageConfiguration feedConfig, IOperationExecutionContext context)
        {
            var client = feedConfig.TryCreateProGetFeedClient(context);

            feedConfig.PackageSourceName = null;
            feedConfig.FeedName = null; 
            feedConfig.FeedUrl = client.FeedApiEndpointUrl;

            if (client.Credentials is TokenCredentials tcred)
                feedConfig.ApiKey = AH.Unprotect(tcred.Token);
            else
                feedConfig.ApiKey = null;

            if (client.Credentials is UsernamePasswordCredentials ucred)
            {
                feedConfig.UserName = ucred.UserName;
                feedConfig.Password = AH.Unprotect(ucred.Password);
            }
            else
            {
                feedConfig.UserName = null;
                feedConfig.Password = null;
            }              
        }
        public static async Task InstallPackageAsync(this IFeedPackageInstallationConfiguration config, IOperationExecutionContext context, Action<OperationProgress> reportProgress)
        {
            var log = context.Log;
            void setProgress(int? percent, string message) => reportProgress(new OperationProgress(percent, message));

            var client = config.TryCreateProGetFeedClient(context);

            var packageToInstall = await client.FindPackageVersionAsync(config);
            if (packageToInstall == null)
            {
                log.LogError($"Package {config.PackageName} v{config.PackageVersion} was not found.");
                return;
            }
            log.LogInformation($"Package {packageToInstall.FullName} v{packageToInstall.Version} will be installed.");

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
            var targetPath = context.ResolvePath(config.TargetDirectory);

            log.LogDebug($"Ensuring target directory ({targetPath}) exists...");
            await fileOps.CreateDirectoryAsync(targetPath);

            var jobOptions = new InstallPackageJobOptions
            {
                PackageName = packageToInstall.FullName.ToString(),
                PackageVersion = packageToInstall.Version.ToString(),
                TargetPath = targetPath
            };

            if (config.DirectDownload)
            {
                log.LogDebug($"Package will downloaded directly on remote server.");
                jobOptions.FeedApiEndpointUrl = client.FeedApiEndpointUrl;
                jobOptions.ApiKey = (client.Credentials as TokenCredentials)?.Token;
                jobOptions.UserName = (client.Credentials as UsernamePasswordCredentials)?.UserName;
                jobOptions.Password = (client.Credentials as UsernamePasswordCredentials)?.Password;
            }
            else
            {
                var size = packageToInstall.Size == 0 ? 100 * 1024 * 1024 : packageToInstall.Size;

                setProgress(0, "downloading package");
                log.LogInformation("Downloading package...");
                var tempStream = TemporaryStream.Create(size);
                var sourceStream = await client.GetPackageStreamAsync(packageToInstall.FullName, packageToInstall.Version);
                await sourceStream.CopyToAsync(tempStream, 80 * 1024, context.CancellationToken, position => setProgress((int)(100 * position / size), "downloading package"));

                var tempDirectoryName = fileOps.CombinePath(await fileOps.GetBaseWorkingDirectoryAsync().ConfigureAwait(false), Guid.NewGuid().ToString("N"));
                await fileOps.CreateDirectoryAsync(tempDirectoryName);
                var tempZipFileName = tempDirectoryName + ".zip";

                log.LogDebug($"Uploading package as temp file ({tempZipFileName }) on remote server");
                tempStream.Position = 0;
                setProgress(0, "copying package to agent");
                using (var remote = await fileOps.OpenFileAsync(tempZipFileName, FileMode.CreateNew, FileAccess.Write))
                {
                    await tempStream.CopyToAsync(remote, 81920, context.CancellationToken, position => setProgress((int)(100 * position / size), "copying package to agent"));
                }

                log.LogDebug($"Package will installed remotely.");
                jobOptions.PackageFilePath = tempZipFileName;
            }

            var jobExec = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();
            var job = new InstallPackageJob { Options = jobOptions };
            job.MessageLogged += (s, e) => log.Log(e.Level, e.Message);
            job.ProgressChanged += (s, e) => reportProgress(e);
            await jobExec.ExecuteJobAsync(job);

            log.LogInformation($"Package installation complete.");

            if (config.LocalRegistry == LocalRegistryOptions.None)
                return;

            log.LogDebug($"Recording package installation in {config.LocalRegistry} registry...");
            reportProgress(new OperationProgress("recording package installation in machine registry"));
            using (var registry = await RemotePackageRegistry.GetRegistryAsync(context.Agent, false).ConfigureAwait(false))
            {
                var package = new RegisteredPackageModel
                {
                    Group = packageToInstall.Group,
                    Name = packageToInstall.Name,
                    Version = packageToInstall.Version.ToString(),
                    InstallPath = targetPath,
                    FeedUrl = client.FeedApiEndpointUrl,
                    InstallationDate = DateTimeOffset.Now.ToString("o"),
                    InstalledUsing = $"{SDK.ProductName}/{SDK.ProductVersion} (InedoCore/{Extension.Version})"
                };

                await RemotePackageRegistry.registryLock.WaitAsync(context.CancellationToken).ConfigureAwait(false);
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
                    RemotePackageRegistry.registryLock.Release();
                }
            }

        }

        [SlimSerializable]
        private sealed class InstallPackageJobOptions
        {
            [SlimSerializable]
            public string FeedApiEndpointUrl { get; set; }
            [SlimSerializable]
            public string UserName { get; set; }
            [SlimSerializable]
            public SecureString Password { get; set; }
            [SlimSerializable]
            public SecureString ApiKey { get; set; }
            [SlimSerializable]
            public string PackageName { get; set; }
            [SlimSerializable]
            public string PackageVersion { get; set; }
            [SlimSerializable]
            public string PackageFilePath { get; set; }
            [SlimSerializable]
            public string TargetPath { get; set; }

            public SecureCredentials CreateCredentials()
            {
                if (this.ApiKey?.Length > 0)
                    return new TokenCredentials { Token = this.ApiKey };
                else if (!string.IsNullOrEmpty(this.UserName))
                    return new UsernamePasswordCredentials { UserName = this.UserName, Password = this.Password };
                return null;
            }
        }

        private sealed class InstallPackageJob : RemoteJob
        {
            public InstallPackageJobOptions Options { get; set; }

            public override Task<object> ExecuteAsync(CancellationToken cancellationToken)
            {
                if (!string.IsNullOrEmpty(this.Options.PackageFilePath))
                    return InstallFromFile(cancellationToken);

                return this.InstallFromFeed(cancellationToken);
            }
            private async Task<object> InstallFromFile(CancellationToken cancellationToken)
            {
                this.SetProgress(cancellationToken, "installing package");
                this.LogDebug($"Installing package from \"{this.Options.PackageFilePath}\" to \"{this.Options.TargetPath}\"...");
                using var package = new UniversalPackage(this.Options.PackageFilePath);
                await package.ExtractContentItemsAsync(this.Options.TargetPath, cancellationToken);
                this.LogInformation("Package installed from file.");
                return null;
            }
            private async Task<object> InstallFromFeed(CancellationToken cancellationToken)
            {
                this.LogDebug($"Installing package from \"{this.Options.FeedApiEndpointUrl}\" to \"{this.Options.TargetPath}\"...");
                var client = new ProGetFeedClient(this.Options.FeedApiEndpointUrl, this.Options.CreateCredentials(), new LoggerWrapper(this), cancellationToken);

                var packageVersion = await client.FindPackageVersionAsync(this.Options.PackageName, this.Options.PackageVersion);
                if (packageVersion == null)
                {
                    this.LogError($"Package {this.Options.PackageName} v{this.Options.PackageVersion} was not found.");
                    return null;
                }

                long size = packageVersion.Size == 0 ? 100 * 1024 * 1024 : packageVersion.Size;

                this.SetProgress(cancellationToken, "downloading package");
                this.LogDebug("Downloading package...");
                var tempStream = TemporaryStream.Create(size);
                var sourceStream = await client.GetPackageStreamAsync(packageVersion.FullName, packageVersion.Version);
                await sourceStream.CopyToAsync(tempStream, 80 * 1024, cancellationToken, position => this.SetProgress(cancellationToken, "downloading package", (int)(100 * position / size)));
                this.LogInformation("Package downloaded.");
                tempStream.Position = 0;

                this.SetProgress(cancellationToken, "installing package");
                this.LogDebug($"Installing package...");
                using var package = new UniversalPackage(tempStream);
                await package.ExtractContentItemsAsync(this.Options.TargetPath, cancellationToken);
                this.LogInformation("Package installed.");
                return null;
            }


            public event EventHandler<OperationProgress> ProgressChanged;

            private void SetProgress(CancellationToken cancellationToken, string message, int? percent = null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding, true);
                writer.Write(percent ?? -1);
                writer.Write(message ?? string.Empty);

                this.Post(stream.ToArray());
            }
            protected override void DataReceived(byte[] data)
            {
                using var stream = new MemoryStream(data, false);
                using var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
                int percent = reader.ReadInt32();
                var message = reader.ReadString();
                this.ProgressChanged?.Invoke(this, new OperationProgress(AH.NullIf(percent, -1), message));
            }

            public override void Serialize(Stream stream) => SlimBinaryFormatter.Serialize(this.Options, stream);
            public override void Deserialize(Stream stream) => this.Options = (InstallPackageJobOptions)SlimBinaryFormatter.Deserialize(stream);
            public override object DeserializeResponse(Stream stream) => null; // no response
            public override void SerializeResponse(Stream stream, object result) { } // no response

            private sealed class LoggerWrapper : ILogSink
            {
                private readonly ILogger logger;
                public LoggerWrapper(ILogger logger) => this.logger = logger;
                void ILogSink.Log(IMessage message) => this.logger.Log(message.Level, message.Message);
            }


        }
    }
}
