using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("UrlEncode")]
    [Description("Escapes a string for use in a URL.")]
    [Tag("strings")]
    public sealed class UrlEncodeVariableFunction : ScalarVariableFunction
    {
        [DisplayName("text")]
        [Description("The text to escape.")]
        [VariableFunctionParameter(0)]
        public string Text { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => Uri.EscapeDataString(this.Text);
    }
}
