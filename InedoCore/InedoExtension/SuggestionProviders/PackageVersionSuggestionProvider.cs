using System.Runtime.CompilerServices;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageVersionSuggestionProvider : ISuggestionProvider
    {
        public IAsyncEnumerable<string> GetSuggestionsAsync(IComponentConfiguration config, CancellationToken cancellationToken)
        {
            return this.GetSuggestionsAsync(string.Empty, config, cancellationToken);
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