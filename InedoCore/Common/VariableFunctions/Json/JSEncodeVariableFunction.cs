using System.ComponentModel;
using Newtonsoft.Json;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Json
{
    [ScriptAlias("JSEncode")]
    [Description("Encodes a string for use in a JavaScript string literal.")]
    [Tag("json")]
    public sealed class JSEncodeVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The text to encode.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(object context)
        {
            var s = JsonConvert.ToString(this.Text ?? string.Empty);
            return s.Substring(1, s.Length - 2);
        }
    }
}
