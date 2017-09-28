using System.ComponentModel;
using Newtonsoft.Json;
using Inedo.Documentation;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#endif

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
