using Inedo.Extensibility.VariableTemplates;
using Inedo.Serialization;

namespace Inedo.Extensions.ListVariableSources
{
    [Undisclosed]
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

        public override Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            return Task.FromResult(Enumerable.Empty<string>());
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
