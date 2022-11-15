using System.Runtime.CompilerServices;
using Inedo.Extensions.UniversalPackages;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageVersionSuggestionProvider : ISuggestionProvider
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
            yield return "latest";
            yield return "latest-stable";

            var packageName = config["Name"];
            if (string.IsNullOrEmpty(packageName))
                yield break;

            var client = await config.TryCreateProGetFeedClientAsync(cancellationToken);
            if (client == null)
                yield break;

            await foreach (var v in client.ListPackageVersionsAsync(packageName, cancellationToken))
            {
                if (string.IsNullOrEmpty(startsWith) || v.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase))
                    yield return v;
            }
        }
    }
}