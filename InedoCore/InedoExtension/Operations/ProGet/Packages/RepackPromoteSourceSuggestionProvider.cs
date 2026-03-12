using System.Runtime.CompilerServices;
using Inedo.Extensions.PackageSources;

namespace Inedo.Extensions.Operations.ProGet.Packages
{
    internal sealed class RepackPromoteSourceSuggestionProvider : ISuggestionProvider
    {
        public IAsyncEnumerable<string> GetSuggestionsAsync(IComponentConfiguration config, CancellationToken cancellationToken)
        {
            return this.GetSuggestionsAsync(string.Empty, config, cancellationToken);
        }

        public async IAsyncEnumerable<string> GetSuggestionsAsync(string startsWith, IComponentConfiguration config, [EnumeratorCancellation] CancellationToken cancellationToken)
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
            yield return new NpmPackageSourceSuggestionProvider();
            yield return new PyPiPackageSourceSuggestionProvider();
        }
    }
}
