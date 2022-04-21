using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var client = config.TryCreateProGetFeedClient();
            if (client == null)
                return Enumerable.Empty<string>();

            var packages = await client.ListPackagesAsync().ConfigureAwait(false);
            return packages.Select(p => p.FullName.ToString());
        }
    }
}