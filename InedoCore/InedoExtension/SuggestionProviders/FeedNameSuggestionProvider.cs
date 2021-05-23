using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Web;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class FeedNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var client = ProGetFeedClient.TryCreate(config.AsFeedPackageConfiguration(), config.EditorContext as ICredentialResolutionContext ?? CredentialResolutionContext.None);
            if (client == null)
                return Enumerable.Empty<string>();

            return await client.GetFeedNamesAsync().ConfigureAwait(false);
        }
    }
}