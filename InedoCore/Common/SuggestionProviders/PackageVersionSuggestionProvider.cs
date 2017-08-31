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
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Configurations;
using Inedo.Hedgehog.Extensibility.Credentials;
using Inedo.Hedgehog.Extensibility.Operations;
using Inedo.Hedgehog.Extensibility.RaftRepositories;
using Inedo.Hedgehog.Web;
using Inedo.Hedgehog.Web.Controls;
using Inedo.Hedgehog.Web.Controls.Plans;
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

            var credentials = ResourceCredentials.Create<ProGetCredentials>(credentialName);
            var client = new ProGetClient(credentials.Url, feedName, credentials.UserName, AH.Unprotect(credentials.Password));

            var package = await client.GetPackageInfoAsync(PackageName.Parse(packageName));

            return new[] { "latest" }.Concat(package.versions);
        }
    }
}