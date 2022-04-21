using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class FeedNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var client = config.TryCreateProGetFeedClient();
            if (client == null)
                return Enumerable.Empty<string>();

            return await client.GetFeedNamesAsync().ConfigureAwait(false);
        }
    }
}