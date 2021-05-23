using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Extensions.UniversalPackages;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var client = ProGetFeedClient.TryCreate(config.AsFeedPackageConfiguration(), config.EditorContext as ICredentialResolutionContext ?? CredentialResolutionContext.None);
            if (client == null)
                return Enumerable.Empty<string>();

            var packages = await client.ListPackagesAsync().ConfigureAwait(false);
            return packages.Select(p => p.FullName.ToString());
        }
    }
}