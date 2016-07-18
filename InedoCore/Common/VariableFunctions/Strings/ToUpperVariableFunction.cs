using System.ComponentModel;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("ToUpper")]
    [Description("Returns a string with all letters converted to uppercase.")]
    [SeeAlso(typeof(ToLowerVariableFunction))]
    [Tag("strings")]
    public sealed class ToUpperVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The string to convert to uppercase.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(object context) => this.Text.ToUpperInvariant();
    }
}
