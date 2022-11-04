using System.Runtime.CompilerServices;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var list = new List<string>();

            await foreach (var p in this.GetSuggestionsAsync(string.Empty, config, default))
                list.Add(p);

            return list;
        }
        public async IAsyncEnumerable<string> GetSuggestionsAsync(string startsWith, IComponentConfiguration config, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var client = await config.TryCreateProGetFeedClientAsync(cancellationToken);
            if (client == null)
                yield break;

            await foreach (var p in client.ListPackagesAsync(cancellationToken: cancellationToken))
            {
                if (string.IsNullOrEmpty(startsWith) || p.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase))
                    yield return p;
            }
        }
    }
}