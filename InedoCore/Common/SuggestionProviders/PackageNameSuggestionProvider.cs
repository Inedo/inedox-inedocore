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
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.RaftRepositories;
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

            var credentials = ResourceCredentials.Create<ProGetCredentials>(credentialName);
            var client = new ProGetClient(credentials.Url, feedName, credentials.UserName, AH.Unprotect(credentials.Password));

            var packages = await client.GetPackagesAsync().ConfigureAwait(false);

            return from p in packages
                   let name = new PackageName(p.@group, p.name)
                   orderby name.Group, name.Name
                   select name.ToString();
        }
    }
}