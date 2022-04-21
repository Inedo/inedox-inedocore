using System.ComponentModel;
using System.Text.Json;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("JSEncode")]
    [Description("Encodes a string for use as a JavaScript literal.")]
    [Tag("strings")]
    public sealed class JSEncodeVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        public string Text { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => JsonSerializer.Serialize(this.Text ?? string.Empty);
    }
}
