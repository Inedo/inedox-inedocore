using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
using Newtonsoft.Json;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("JSEncode")]
    [Description("Encodes a string for use as a JavaScript literal.")]
    [Tag("strings")]
    public sealed class JSEncodeVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        public string Text { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => JsonConvert.ToString(this.Text ?? string.Empty);
    }
}
