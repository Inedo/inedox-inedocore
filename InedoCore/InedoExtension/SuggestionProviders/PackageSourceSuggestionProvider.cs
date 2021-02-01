using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageSourceSuggestionProvider : ISuggestionProvider
    {
        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var result = SDK.GetPackageSources()
                .OrderBy(s => s.ResourceInfo.Name)
                .Select(s => s.ResourceInfo.Name);

            return Task.FromResult(result);
        }
    }
}
