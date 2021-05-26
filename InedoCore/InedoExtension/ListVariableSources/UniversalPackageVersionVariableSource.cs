using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Extensions.SecureResources;
using Inedo.Serialization;
using Inedo.Web;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.ListVariableSources
{
    [DisplayName("Universal Package Versions")]
    [Description("Versions of a specific universal package from a universal package feed.")]
    public sealed class UniversalPackageVersionVariableSource : DynamicListVariableType
    {
        [Persistent]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public string PackageSourceName { get; set; }
        [Required]
        [Persistent]
        [DisplayName("Package name")]
        [Description("The full name of the package in the format Group/Name.")]
        public string PackageName { get; set; }
        [Persistent]
        [DisplayName("Include prerelease versions")]
        public bool IncludePrerelease { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var credContext = new CredentialResolutionContext(context.ProjectId, null);
            var packageSource = SecureResource.TryCreate(this.PackageSourceName, credContext) as UniversalPackageSource;
            if (packageSource == null)
                return Enumerable.Empty<string>();

            var client = new ProGetFeedClient(packageSource.ApiEndpointUrl, packageSource.GetCredentials(credContext));
            
            return (await client.ListPackageVersionsAsync(this.PackageName).ConfigureAwait(false))
                .Where(p => this.IncludePrerelease || string.IsNullOrEmpty(p.Version.Prerelease))
                .OrderByDescending(p => p.Version)
                .Select(p => p.Version.ToString());
        }
        public override RichDescription GetDescription()
        {
            return new RichDescription(
                "Versions of ",
                new Hilite(this.PackageName),
                " from ",
                new Hilite(this.PackageSourceName),
                "."
            );
        }
    }
}
