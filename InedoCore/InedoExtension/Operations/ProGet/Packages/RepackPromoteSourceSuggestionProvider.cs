using System.Runtime.CompilerServices;
using Inedo.Extensions.PackageSources;

namespace Inedo.Extensions.Operations.ProGet.Packages
{
    internal sealed class RepackPromoteSourceSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var results = new List<string>();

            foreach (var p in GetSuggestionProviders())
            {
                foreach (var v in await p.GetSuggestionsAsync(config).ConfigureAwait(false))
                    results.Add(v);
            }

            return results;
        }

        async IAsyncEnumerable<string> ISuggestionProvider.GetSuggestionsAsync(string startsWith, IComponentConfiguration config, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var p in GetSuggestionProviders())
            {
                await foreach (var v in p.GetSuggestionsAsync(startsWith, config, cancellationToken).ConfigureAwait(false))
                    yield return v;
            }
        }

        private static IEnumerable<ISuggestionProvider> GetSuggestionProviders()
        {
            yield return new NuGetPackageSourceSuggestionProvider();
            yield return new UniversalPackageSourceSuggestionProvider();
        }
    }
}
