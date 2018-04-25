using System.ComponentModel;
using System.Net;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("HtmlEncode")]
    [Description("Encodes a string for use in HTML.")]
    [Tag("strings")]
    public sealed class HtmlEncodeVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The text to encode.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => WebUtility.HtmlEncode(this.Text);
    }
}
