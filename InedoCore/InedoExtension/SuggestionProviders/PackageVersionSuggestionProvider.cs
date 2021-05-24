using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Extensions.UniversalPackages;
using Inedo.Web;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageVersionSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var packageName = config[nameof(IFeedPackageConfiguration.PackageName)];
            if (string.IsNullOrEmpty(packageName))
                return Enumerable.Empty<string>();

            var client = config.TryCreateProGetFeedClient();
            if (client == null)
                return Enumerable.Empty<string>();


            var package = await client.ListPackageVersionsAsync(packageName).ConfigureAwait(false);

            var options = SDK.ProductName == "BuildMaster"
                ? new[] { "attached", "latest", "latest-stable" }
                : new[] { "latest", "latest-stable" };

            return options.Concat(package.OrderByDescending(p => p.Version).Select(v => v.Version.ToString()));
        }
    }
}