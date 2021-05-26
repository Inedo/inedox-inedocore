using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Extensions.SecureResources;
using Inedo.Serialization;
using Inedo.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.ListVariableSources
{
    [DisplayName("Universal Packages")]
    [Description("Universal package names from a universal package feed, optionally filtered by group.")]
    public sealed class UniversalPackageListVariableSource : DynamicListVariableType
    {
        [Persistent]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public string PackageSourceName { get; set; }

        [Persistent]
        [PlaceholderText("all groups")]
        [DisplayName("Show only in group")]
        public string Group { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var credContext = new CredentialResolutionContext(context.ProjectId, null);
            var packageSource = SecureResource.TryCreate(this.PackageSourceName, credContext) as UniversalPackageSource;
            if (packageSource == null)
                return Enumerable.Empty<string>();

            var client = new ProGetFeedClient(packageSource.ApiEndpointUrl, packageSource.GetCredentials(credContext));

            
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
                    new Hilite(this.PackageSourceName),
                    "."
                );
            }
            else
            {
                return new RichDescription(
                    "Packages in ",
                    new Hilite(this.Group),
                    " from ",
                    new Hilite(this.PackageSourceName),
                    "."
                );
            }
        }
    }
}
