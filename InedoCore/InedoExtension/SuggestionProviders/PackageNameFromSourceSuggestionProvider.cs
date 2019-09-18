using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.UPack.Net;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageNameFromSourceSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var sourceName = config["PackageSource"];
            if (string.IsNullOrWhiteSpace(sourceName))
                return Enumerable.Empty<string>();

            var source = SDK.GetPackageSources()
                .FirstOrDefault(s => s.PackageType == AttachedPackageType.Universal && string.Equals(s.Name, sourceName, StringComparison.OrdinalIgnoreCase));

            if (source == null)
                return Enumerable.Empty<string>();

            string userName = null;
            SecureString password = null;

            if (!string.IsNullOrWhiteSpace(source.CredentialName))
            {
                var userNameCredentials = (UsernamePasswordCredentials)ResourceCredentials.TryCreate("UsernamePassword", source.CredentialName, null, null, false);
                if (userNameCredentials != null)
                {
                    userName = userNameCredentials.UserName;
                    password = userNameCredentials.Password;
                }
                else
                {
                    var productCredentials = (InedoProductCredentials)ResourceCredentials.TryCreate("InedoProduct", source.CredentialName, null, null, false);
                    if (productCredentials != null)
                    {
                        userName = "api";
                        password = productCredentials.ApiKey;
                    }
                }
            }

            var client = new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(source.FeedUrl), userName, password));
            return (await client.ListPackagesAsync(null, 200))
                .OrderBy(p => p.Group ?? string.Empty)
                .ThenBy(p => p.Name)
                .Select(p => p.FullName.ToString())
                .ToList();
        }
    }
}
