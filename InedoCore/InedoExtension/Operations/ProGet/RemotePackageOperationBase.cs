using System;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;

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
    }
}
