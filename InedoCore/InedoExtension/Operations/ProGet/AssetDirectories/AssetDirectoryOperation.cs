using Inedo.AssetDirectories;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.PackageSources;

#nullable enable

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    public abstract class AssetDirectoryOperation : ExecuteOperation
    {
        private volatile int percent = -1;
        private volatile string? message;

        private protected AssetDirectoryOperation()
        {
        }

        public abstract string? Path { get; set; }

        [ScriptAlias("Source")]
        [DisplayName("Source")]
        [SuggestableValue(typeof(AssetSourceSuggestionProvider))]
        public string? AssetSourceId { get; set; }
        [ScriptAlias("Resource")]
        [Category("Connection/Identity")]
        [DisplayName("Secure resource (legacy)")]
        public string? ResourceName { get; set; }
        [ScriptAlias("EndpointUrl")]
        [DisplayName("API endpoint URL")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use URL from secure resource")]
        public string? ApiUrl { get; set; }
        [ScriptAlias("ApiKey")]
        [DisplayName("API key")]
        [Category("Connection/Identity")]
        [FieldEditMode(FieldEditMode.Password)]
        [PlaceholderText("Use token from secure credentials")]
        public string? ApiKey { get; set; }
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use user name from secure credentials")]
        public string? UserName { get; set; }
        [ScriptAlias("Password")]
        [Category("Connection/Identity")]
        [FieldEditMode(FieldEditMode.Password)]
        [PlaceholderText("Use password from secure credentials")]
        public string? Password { get; set; }

#pragma warning disable CS0618 // Type or member is obsolete
        private string? LegacySecureResourceName => this.ResourceName;
#pragma warning restore CS0618 // Type or member is obsolete

        public override OperationProgress GetProgress() => new(AH.NullIf(this.percent, -1), this.message);
        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrEmpty(this.AssetSourceId))
            {
                var id = new AssetSourceId(this.AssetSourceId);
                switch (id.Format)
                {
                    case AssetSourceIdFormat.SecureResource:
                        this.GetLegacySecureResource(context, id.GetResourceName());
                        break;

                    case AssetSourceIdFormat.ProGetAssetDirectory:
                        this.GetCredentials(context, id.GetProGetServiceCredentialName(), id.GetAssetDirectoryName());
                        break;

                    default:
                        this.LogError($"Unexpected source format: {id.Format}");
                        return Complete;
                }
            }
            else if (!string.IsNullOrWhiteSpace(this.LegacySecureResourceName))
            {
                this.GetLegacySecureResource(context, this.LegacySecureResourceName);
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

        private void GetLegacySecureResource(ICredentialResolutionContext context, string secureResourceName)
        {
            if (!context.TryGetSecureResource(SecureResourceType.General, secureResourceName, out var r) || r is not ProGetAssetDirectorySecureResource resource)
                throw new ExecutionFailureException($"Resource {secureResourceName} is not a ProGet secure resource.");

            if (string.IsNullOrWhiteSpace(this.ApiUrl))
                this.ApiUrl = resource.EndpointUrl;

            if (!string.IsNullOrWhiteSpace(resource.RootFolder))
                this.Path = PathEx.Combine('/', resource.RootFolder, this.Path ?? string.Empty);

            switch (resource.GetCredentials(context))
            {
                case TokenCredentials tokenCredentials:
                    if (string.IsNullOrWhiteSpace(this.ApiKey))
                        this.ApiKey = AH.Unprotect(tokenCredentials.Token);
                    break;
                case UsernamePasswordCredentials userCredentials:
                    if (string.IsNullOrWhiteSpace(this.UserName))
                        this.UserName = userCredentials.UserName;
                    if (string.IsNullOrEmpty(this.Password))
                        this.Password = AH.Unprotect(userCredentials.Password);
                    break;
            }
        }
        private bool GetCredentials(ICredentialResolutionContext context, string credentialName, string feedName)
        {
            var creds = SecureCredentials.TryCreate(credentialName, context);
            if (creds is not ProGetServiceCredentials credentials)
                throw new ExecutionFailureException($"Credential {credentialName} is not a ProGet service credential.");

            if (!string.IsNullOrEmpty(this.ApiUrl) && !string.IsNullOrEmpty(credentials.ServiceUrl))
                this.ApiUrl = PathEx.Combine('/', credentials.ServiceUrl, Uri.EscapeDataString(feedName));

            if (!string.IsNullOrEmpty(this.ApiKey))
                this.ApiKey = credentials.APIKey;

            if (!string.IsNullOrEmpty(this.UserName))
                this.UserName = credentials.UserName;

            if (!string.IsNullOrEmpty(this.Password))
                this.Password = credentials.Password;

            return true;
        }
    }
}
