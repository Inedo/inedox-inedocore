using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensions.Operations.ProGet;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions.Credentials;
using Inedo.Otter.Web.Controls;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Web;
#endif

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var feedName = config["FeedName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(feedName))
                return Enumerable.Empty<string>();

            ProGetClient client = null;

#if Hedgehog
            var productCredentials = ResourceCredentials.TryCreate<InedoProductCredentials>(credentialName);
            if (productCredentials != null)
                client = new ProGetClient(productCredentials.Host, feedName, "api", AH.Unprotect(productCredentials.ApiKey));
#endif

            if (client == null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                var credentials = ResourceCredentials.Create<ProGetCredentials>(credentialName);
#pragma warning restore CS0618 // Type or member is obsolete
                client = new ProGetClient(credentials.Url, feedName, credentials.UserName, AH.Unprotect(credentials.Password));
            }

            var packages = await client.GetPackagesAsync().ConfigureAwait(false);

            return from p in packages
                   let name = new PackageName(p.@group, p.name)
                   orderby name.Group, name.Name
                   select name.ToString();
        }
    }
}