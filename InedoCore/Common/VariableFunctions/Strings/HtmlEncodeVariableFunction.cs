using System.ComponentModel;
using System.Net;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("HtmlEncode")]
    [Description("Encodes a string for use in HTML.")]
    [Tag("strings")]
    public sealed class HtmlEncodeVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The text to encode.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(object context) => WebUtility.HtmlEncode(this.Text);
    }
}
