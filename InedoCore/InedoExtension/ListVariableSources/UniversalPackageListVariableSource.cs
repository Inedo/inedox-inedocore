using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Serialization;
using Inedo.UPack.Net;

namespace Inedo.Extensions.ListVariableSources
{
    [DisplayName("Universal Packages")]
    [Description("Universal package names from a universal package feed, optionally filtered by group.")]
    public sealed class UniversalPackageListVariableSource : ListVariableSource
    {
        [Persistent]
        [PlaceholderText("any group")]
        public string Group { get; set; }
        [Persistent]
        [DisplayName("Feed URL")]
        public string FeedUrl { get; set; }
        [Persistent]
        [DisplayName("Credentials")]
        [Description("Specify the name of an InedoProductCredentials resource to use for authentication.")]
        [PlaceholderText("no authentication")]
        public string CredentialName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            var client = new UniversalFeedClient(this.GetEndpoint());

            return (await client.ListPackagesAsync(this.Group, 100).ConfigureAwait(false))
                // additional level of filtering in case of bugs in server
                .Where(p => string.IsNullOrEmpty(this.Group) || string.Equals(this.Group, p.Group, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name)
                .Select(p => p.Name);
        }
        public override RichDescription GetDescription()
        {
            if (string.IsNullOrEmpty(this.Group))
            {
                return new RichDescription(
                    "Packages from ",
                    new Hilite(AH.CoalesceString(this.FeedUrl, this.CredentialName)),
                    "."
                );
            }
            else
            {
                return new RichDescription(
                    "Packages in ",
                    new Hilite(this.Group),
                    " from ",
                    new Hilite(AH.CoalesceString(this.FeedUrl, this.CredentialName)),
                    "."
                );
            }
        }

        private UniversalFeedEndpoint GetEndpoint()
        {
            if (string.IsNullOrWhiteSpace(this.CredentialName))
                return new UniversalFeedEndpoint(this.FeedUrl, true);

            var credentials = ResourceCredentials.Create<InedoProductCredentials>(this.CredentialName);

            var url = AH.CoalesceString(this.FeedUrl, credentials.Host);

            if (credentials.ApiKey != null && credentials.ApiKey.Length > 0)
                return new UniversalFeedEndpoint(new Uri(url), "api", credentials.ApiKey);

            return new UniversalFeedEndpoint(url, true);
        }
    }
}
