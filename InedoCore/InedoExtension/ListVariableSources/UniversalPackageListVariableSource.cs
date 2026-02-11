using Inedo.Extensibility.VariableTemplates;
using Inedo.Serialization;

namespace Inedo.Extensions.ListVariableSources
{
    [Undisclosed]
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

        public override IAsyncEnumerable<string> EnumerateListValuesAsync(VariableTemplateContext context) => AsyncEnumerable.Empty<string>();
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
