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
    internal sealed class PackageVersionSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var feedName = config["FeedName"];
            var packageName = config["PackageName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(feedName) || string.IsNullOrEmpty(packageName))
                return Enumerable.Empty<string>();

            ProGetClient client = null;

#if Hedgehog
            var productCredentials = ResourceCredentials.TryCreate<InedoProductCredentials>(credentialName);
            if (productCredentials != null)
                client = new ProGetClient(productCredentials.Host, feedName, "api", AH.Unprotect(productCredentials.ApiKey));
#endif


#pragma warning disable CS0618 // Type or member is obsolete
            var credentials = ResourceCredentials.Create<ProGetCredentials>(credentialName);
#pragma warning restore CS0618 // Type or member is obsolete
            if (client == null)
                client = new ProGetClient(credentials.Url, feedName, credentials.UserName, AH.Unprotect(credentials.Password));

            var package = await client.GetPackageInfoAsync(PackageName.Parse(packageName));

            return new[] { "latest" }.Concat(package.versions);
        }
    }
}