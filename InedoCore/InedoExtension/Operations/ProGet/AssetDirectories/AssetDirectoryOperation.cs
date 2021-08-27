using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.AssetDirectories;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.SecureResources;
using Inedo.IO;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    public abstract class AssetDirectoryOperation : ExecuteOperation
    {
        private volatile int percent = -1;
        private volatile string message;

        private protected AssetDirectoryOperation()
        {
        }

        public abstract string Path { get; set; }
        [ScriptAlias("Resource")]
        [DisplayName("Secure resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<ProGetAssetDirectorySecureResource>))]
        public string ResourceName { get; set; }
        [ScriptAlias("EndpointUrl")]
        [DisplayName("API endpoint URL")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use URL from secure resource")]
        public string ApiUrl { get; set; }
        [ScriptAlias("ApiKey")]
        [DisplayName("API key")]
        [Category("Connection/Identity")]
        [FieldEditMode(FieldEditMode.Password)]
        [PlaceholderText("Use token from secure credentials")]
        public string ApiKey { get; set; }
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use user name from secure credentials")]
        public string UserName { get; set; }
        [ScriptAlias("Password")]
        [Category("Connection/Identity")]
        [FieldEditMode(FieldEditMode.Password)]
        [PlaceholderText("Use password from secure credentials")]
        public string Password { get; set; }

        public override OperationProgress GetProgress() => new(AH.NullIf(this.percent, -1), this.message);
        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(this.ResourceName))
            {
                if (SecureResource.TryCreate(this.ResourceName, (IResourceResolutionContext)context) is not ProGetAssetDirectorySecureResource resource)
                {
                    this.LogError($"Resource {this.ResourceName} is not a ProGet Asset Directory secure resource.");
                    return Complete;
                }

                if (string.IsNullOrWhiteSpace(this.ApiUrl))
                    this.ApiUrl = resource.EndpointUrl;

                if (!string.IsNullOrWhiteSpace(resource.RootFolder))
                    this.Path = PathEx.Combine('/', resource.RootFolder, this.Path ?? string.Empty);

                switch (resource.GetCredentials((ICredentialResolutionContext)context))
                {
                    case Credentials.TokenCredentials tokenCredentials:
                        if (string.IsNullOrWhiteSpace(this.ApiKey))
                            this.ApiKey = AH.Unprotect(tokenCredentials.Token);
                        break;
                    case Credentials.UsernamePasswordCredentials userCredentials:
                        if (string.IsNullOrWhiteSpace(this.UserName))
                            this.UserName = userCredentials.UserName;
                        if (string.IsNullOrEmpty(this.Password))
                            this.Password = AH.Unprotect(userCredentials.Password);
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(this.ApiUrl))
            {
                this.LogError("EndpointUrl must be specified in either the operation or the referenced secure resource.");
                return Complete;
            }

            var client = new AssetDirectoryClient(this.ApiUrl, AH.NullIf(this.ApiKey, string.Empty), AH.NullIf(this.UserName, string.Empty), AH.NullIf(this.Password, string.Empty));
            return this.ExecuteAsync(client, context);
        }

        private protected abstract Task ExecuteAsync(AssetDirectoryClient client, IOperationExecutionContext context);
        private protected void ProgressReceived(int? percent, string message)
        {
            this.percent = percent ?? -1;
            this.message = message;
        }
    }
}
