using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using Inedo.UPack;
using Inedo.UPack.Net;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    public abstract class RemotePackageOperationBase : RemoteExecuteOperation
    {
        protected RemotePackageOperationBase()
        {
        }

        public abstract string PackageName { get; set; }

        public abstract string PackageSource { get; set; }

        [field: NonSerialized]
        private protected IPackageManager PackageManager { get; private set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            string userName = null;
            SecureString password = null;
            string feedUrl = null;
            await base.BeforeRemoteExecuteAsync(context);
            this.PackageManager = await context.TryGetServiceAsync<IPackageManager>();

            // if package source not specified, try to infer it from the package name
            if (string.IsNullOrEmpty(this.PackageSource) && this.PackageManager != null)
                this.PackageSource = (await this.PackageManager.GetBuildPackagesAsync(context.CancellationToken))
                    .FirstOrDefault(p => string.Equals(p.Name, this.PackageName, StringComparison.OrdinalIgnoreCase))
                    ?.PackageSource;

            // if package source is specified, look up the info while still executing locally
            if (!string.IsNullOrEmpty(this.PackageSource))
                this.ResolvePackageSource(context, this.PackageSource, out userName, out password, out feedUrl);

            this.SetPackageSourceProperties(userName, AH.Unprotect(password), feedUrl);
        }

        private protected abstract void SetPackageSourceProperties(string userName, string password, string feedUrl);

        private protected static string GetFullPackageName(string group, string name) => string.IsNullOrWhiteSpace(group) ? name : (group + "/" + name);

        private protected async Task<byte[]> UploadVirtualAndComputeHashAsync(string fileName, string feedUrl, string userName, SecureString password, CancellationToken cancellationToken)
        {
            var tempFileName = Path.GetTempFileName();
            try
            {
                using (var vpackStream = File.OpenRead(fileName))
                using (var tempZip = new ZipArchive(File.Create(tempFileName), ZipArchiveMode.Create))
                {
                    var upackEntry = tempZip.CreateEntry("upack.json");
                    using (var upackStream = upackEntry.Open())
                    {
                        vpackStream.CopyTo(upackStream);
                    }
                }

                return await this.UploadAndComputeHashAsync(tempFileName, feedUrl, userName, password, cancellationToken);
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }
        private protected async Task<byte[]> UploadAndComputeHashAsync(string fileName, string feedUrl, string userName, SecureString password, CancellationToken cancellationToken)
        {
            // start computing the hash in the background
            var computeHashTask = Task.Factory.StartNew(computePackageHash, TaskCreationOptions.LongRunning);

            this.LogDebug("Package source URL: " + feedUrl);
            this.LogInformation($"Uploading package to {this.PackageSource}...");

            using (var fileStream = FileEx.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan | FileOptions.Asynchronous))
            {
                var client = new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(feedUrl), userName, password));
                await client.UploadPackageAsync(fileStream, cancellationToken);
            }

            this.LogDebug("Package uploaded.");

            this.LogDebug("Waiting for package hash to be computed...");
            var hash = await computeHashTask;
            this.LogDebug("Package SHA1: " + new HexString(hash));
            return hash;

            byte[] computePackageHash()
            {
                using (var fileStream = FileEx.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan))
                using (var sha1 = SHA1.Create())
                {
                    return sha1.ComputeHash(fileStream);
                }
            }
        }

        private protected void ResolvePackageSource(IOperationExecutionContext context, string name, out string userName, out SecureString password, out string feedUrl)
        {
            var packageSource = SDK.GetPackageSources()
                .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

            if (packageSource == null)
                throw new ExecutionFailureException($"Package source \"{name}\" not found.");

            feedUrl = packageSource.FeedUrl;
            userName = null;
            password = null;

            if (!string.IsNullOrEmpty(packageSource.CredentialName))
            {
                int? applicationId = null;
                int? environmentId = null;

                this.LogDebug($"Looking up credentials ({packageSource.CredentialName})...");

                if (context is IStandardContext standardContext)
                {
                    applicationId = standardContext.ProjectId;
                    environmentId = standardContext.EnvironmentId;
                }

                var userNameCredentials = (UsernamePasswordCredentials)ResourceCredentials.TryCreate("UsernamePassword", packageSource.CredentialName, environmentId, applicationId, false);
                if (userNameCredentials != null)
                {
                    userName = userNameCredentials.UserName;
                    password = userNameCredentials.Password;
                }
                else
                {
                    var productCredentials = (InedoProductCredentials)ResourceCredentials.TryCreate("InedoProduct", packageSource.CredentialName, environmentId, applicationId, false);
                    if (productCredentials == null)
                        throw new ExecutionFailureException($"Credentials ({packageSource.CredentialName}) specified in \"{packageSource.Name}\" package source must be Inedo Product credentials or Username & Password credentials.");

                    if ((productCredentials.Products & InedoProduct.ProGet) == 0)
                        this.LogWarning($"Inedo Product credentials ({packageSource.CredentialName}) specified in \"{packageSource.Name}\" package source are not marked as ProGet credentials.");

                    userName = "api";
                    password = productCredentials.ApiKey;
                }
            }
        }
    }
}
