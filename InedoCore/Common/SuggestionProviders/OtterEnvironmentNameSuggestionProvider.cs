using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensions.Operations.Otter;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
using CredentialsType = Inedo.BuildMaster.Extensibility.Credentials.OtterCredentials;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions.Credentials;
using Inedo.Otter.Web.Controls;
using CredentialsType = Inedo.Otter.Extensions.Credentials.OtterCredentials;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Web;
using CredentialsType = Inedo.Extensibility.Credentials.InedoProductCredentials;
#endif

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class OtterEnvironmentNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            string credentialName = config["CredentialName"];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<CredentialsType>(credentialName);

            var client = OtterClient.Create(credentials.Host, credentials.ApiKey);
            var servers = await client.EnumerateInfrastructureAsync(InfrastructureEntity.Environment).ConfigureAwait(false);

            return servers;
        }
    }
}
