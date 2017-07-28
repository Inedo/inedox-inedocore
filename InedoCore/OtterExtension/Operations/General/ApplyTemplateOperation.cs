using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Web.Controls;

namespace Inedo.Extensions.Operations.General
{
    [DefaultProperty(nameof(Asset))]
    [Description("Applies full template transformation on a literal, a file, or a template asset.")]
    [Example(@"
# applies the hdars template and stores the result in $text
Apply-Template hdars
(
    OutputVariable => $text
);
")]
    partial class ApplyTemplateOperation
    {
        [ScriptAlias("Asset")]
        [SuggestibleValue(typeof(TextTemplateRaftSuggestionProvider))]
        [PlaceholderText("not using an asset")]
        public string Asset { get; set; }
    }
}
