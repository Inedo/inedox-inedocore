using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.SecureResources;
using Inedo.UPack.Net;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
#warning unincluse
    internal sealed class PackageNameFromSourceSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var sourceName = config["PackageSource"];
            if (string.IsNullOrWhiteSpace(sourceName))
                return Enumerable.Empty<string>();

            var source = (UniversalPackageSource)SecureResource.TryCreate(sourceName, new ResourceResolutionContext(null));
            if (source == null)
                return Enumerable.Empty<string>();

            string userName = null;
            SecureString password = null;

            var creds = source.GetCredentials(new CredentialResolutionContext(null, null));
            if (creds != null)
            {
                if (creds is TokenCredentials tc)
                {
                    userName = "api";
                    password = tc.Token;
                }
                else if (creds is Inedo.Extensions.Credentials.UsernamePasswordCredentials upc)
                {
                    userName = upc.UserName;
                    password = upc.Password;
                }
                else
                    throw new InvalidOperationException();
            }

            var client = new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(source.ApiEndpointUrl), userName, password));
            return (await client.ListPackagesAsync(null, 200))
                .OrderBy(p => p.Group ?? string.Empty)
                .ThenBy(p => p.Name)
                .Select(p => p.FullName.ToString())
                .ToList();
        }
    }
}
