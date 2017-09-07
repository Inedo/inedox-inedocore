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
    [ScriptAlias("JSEncode")]
    [Description("Encodes a string for use as a JavaScript literal.")]
    [Tag("strings")]
    public sealed class JSEncodeVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        public string Text { get; set; }

        protected override object EvaluateScalar(object context) => InedoLib.Util.JavaScript.JsonEncode(this.Text);
    }
}
