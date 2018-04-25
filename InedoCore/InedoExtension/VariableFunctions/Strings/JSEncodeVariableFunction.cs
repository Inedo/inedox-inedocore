using System.ComponentModel;
using System.Web;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("JSEncode")]
    [Description("Encodes a string for use as a JavaScript literal.")]
    [Tag("strings")]
    public sealed class JSEncodeVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        public string Text { get; set; }

        protected override object EvaluateScalar(object context) => HttpUtility.JavaScriptStringEncode(this.Text ?? string.Empty);
    }
}
