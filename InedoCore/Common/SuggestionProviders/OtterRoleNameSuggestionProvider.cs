using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensions.Operations.Otter;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions.Credentials;
using Inedo.Otter.Web.Controls;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Configurations;
using Inedo.Hedgehog.Extensibility.Credentials;
using Inedo.Hedgehog.Extensibility.Operations;
using Inedo.Hedgehog.Extensibility.RaftRepositories;
using Inedo.Hedgehog.Web;
using Inedo.Hedgehog.Web.Controls;
using Inedo.Hedgehog.Web.Controls.Plans;
#endif

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class OtterRoleNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            string credentialName = config["CredentialName"];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<OtterCredentials>(credentialName);

            var client = OtterClient.Create(credentials.Host, credentials.ApiKey);
            var roles = await client.EnumerateInfrastructureAsync(InfrastructureEntity.Role).ConfigureAwait(false);

            return roles;
        }
    }
}
