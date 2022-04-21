using System.ComponentModel;
using System.Text.Json.Nodes;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Json
{
    [ScriptAlias("JSEncode")]
    [Description("Encodes a string for use in a JavaScript string literal.")]
    [Tag("json")]
    public sealed class JSEncodeVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The text to encode.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            var value = JsonValue.Create(this.Text ?? string.Empty);
            var s = value.ToJsonString();
            return s[1..^1];
        }
    }
}
