using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.BuildMaster.Data;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class OtterCredentialSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            string name = typeof(InedoProductCredentials).GetCustomAttribute<ScriptAliasAttribute>()?.Alias;
            var credentials = from c in (await new DB.Context(false).Credentials_GetCredentialsAsync().ConfigureAwait(false))
                              where name == null || c.CredentialType_Name == name
                              select c.Credential_Name;

            return credentials;
        }
    }
}
