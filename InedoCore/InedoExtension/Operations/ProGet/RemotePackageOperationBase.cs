using System;
using System.IO;
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

        public abstract string PackageSource { get; set; }

        [field: NonSerialized]
        private protected IPackageManager PackageManager { get; private set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            string userName = null;
            string password = null;
            string feedUrl = null;
            await base.BeforeRemoteExecuteAsync(context);
            this.PackageManager = await context.TryGetServiceAsync<IPackageManager>();

            // if package source is specified, look up the info while still executing locally
            if (!string.IsNullOrEmpty(this.PackageSource))
            {
                var packageSource = SDK.GetPackageSources()
                    .FirstOrDefault(s => string.Equals(s.Name, this.PackageSource, StringComparison.OrdinalIgnoreCase));

                if (packageSource == null)
                    throw new ExecutionFailureException($"Package source \"{this.PackageSource}\" not found.");

                feedUrl = packageSource.FeedUrl;

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
                        // assign these values to the operation so they get serialized prior to remote execute
                        userName = userNameCredentials.UserName;
                        password = AH.Unprotect(userNameCredentials.Password);
                    }
                    else
                    {
                        var productCredentials = (InedoProductCredentials)ResourceCredentials.TryCreate("InedoProduct", packageSource.CredentialName, environmentId, applicationId, false);
                        if (productCredentials == null)
                            throw new ExecutionFailureException($"Credentials ({packageSource.CredentialName}) specified in \"{packageSource.Name}\" package source must be Inedo Product credentials or Username & Password credentials.");

                        if ((productCredentials.Products & InedoProduct.ProGet) == 0)
                            this.LogWarning($"Inedo Product credentials ({packageSource.CredentialName}) specified in \"{packageSource.Name}\" package source are not marked as ProGet credentials.");

                        // assign these values to the operation so they get serialized prior to remote execute
                        userName = "api";
                        password = AH.Unprotect(productCredentials.ApiKey);
                    }
                }
            }

            this.SetPackageSourceProperties(userName, password, feedUrl);
        }

        private protected abstract void SetPackageSourceProperties(string userName, string password, string feedUrl);

        private protected static string GetFullPackageName(string group, string name) => string.IsNullOrWhiteSpace(group) ? name : (group + "/" + name);

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
    }
}
