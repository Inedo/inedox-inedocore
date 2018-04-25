using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class FeedNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var productCredentials = ResourceCredentials.TryCreate<InedoProductCredentials>(credentialName);
            if (productCredentials != null)
            {
                var url = new Uri(productCredentials.Host, UriKind.Absolute).GetLeftPart(UriPartial.Authority);
                var c = new ProGetClient(url, null, "api", AH.Unprotect(productCredentials.ApiKey));

                return await c.GetFeedNamesAsync().ConfigureAwait(false);
            }

#pragma warning disable CS0618 // Type or member is obsolete
            var credentials = ResourceCredentials.Create<ProGetCredentials>(credentialName);
#pragma warning restore CS0618 // Type or member is obsolete
            string baseUrl = new Uri(credentials.Url, UriKind.Absolute).GetLeftPart(UriPartial.Authority);
            var client = new ProGetClient(baseUrl, null, credentials.UserName, AH.Unprotect(credentials.Password));

            return await client.GetFeedNamesAsync().ConfigureAwait(false);
        }
    }
}