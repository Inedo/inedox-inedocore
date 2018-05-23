using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Serialization;
using Inedo.UPack;
using Inedo.UPack.Net;

namespace Inedo.Extensions.ListVariableSources
{
    [DisplayName("Universal Package Versions")]
    [Description("Versions of a specific universal package from a universal package feed.")]
    public sealed class UniversalPackageVersionVariableSource : ListVariableSource
    {
        [Required]
        [Persistent]
        [DisplayName("Package")]
        [Description("The full name of the package in the format Group/Name.")]
        public string PackageId { get; set; }
        [Persistent]
        [DisplayName("Feed URL")]
        public string FeedUrl { get; set; }
        [Persistent]
        [DisplayName("Credentials")]
        [Description("Specify the name of an InedoProductCredentials resource to use for authentication.")]
        [PlaceholderText("no authentication")]
        public string CredentialName { get; set; }
        [Persistent]
        [DisplayName("Include prerelease versions")]
        public bool IncludePrerelease { get; set; }

        public override async Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            var id = UniversalPackageId.Parse(this.PackageId);

            var client = new UniversalFeedClient(this.GetEndpoint());

            return (await client.ListPackageVersionsAsync(id, false).ConfigureAwait(false))
                .Where(p => this.IncludePrerelease || string.IsNullOrEmpty(p.Version.Prerelease))
                .OrderByDescending(p => p.Version)
                .Select(p => p.Version.ToString());
        }
        public override RichDescription GetDescription()
        {
            return new RichDescription(
                "Versions of ",
                new Hilite(this.PackageId),
                " from ",
                new Hilite(AH.CoalesceString(this.FeedUrl, this.CredentialName)),
                "."
            );
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
