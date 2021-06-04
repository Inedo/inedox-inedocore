using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Operations.Otter;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    [Obsolete]
    internal sealed class OtterRoleNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            string credentialName = config["CredentialName"];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<InedoProductCredentials>(credentialName);

            var client = OtterClient.Create(credentials.Host, credentials.ApiKey);
            var roles = await client.EnumerateInfrastructureAsync(InfrastructureEntity.Role).ConfigureAwait(false);

            return roles;
        }
    }
}
