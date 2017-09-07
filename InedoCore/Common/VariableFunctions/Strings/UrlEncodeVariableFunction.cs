using System;
using System.ComponentModel;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("UrlEncode")]
    [Description("Escapes a string for use in a URL.")]
    [Tag("strings")]
    public sealed class UrlEncodeVariableFunction : CommonScalarVariableFunction
    {
        [DisplayName("text")]
        [Description("The text to escape.")]
        [VariableFunctionParameter(0)]
        public string Text { get; set; }

        protected override object EvaluateScalar(object context) => Uri.EscapeDataString(this.Text);
    }
}
