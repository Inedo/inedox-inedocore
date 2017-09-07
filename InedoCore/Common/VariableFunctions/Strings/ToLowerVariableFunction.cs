using System.ComponentModel;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("ToLower")]
    [Description("Returns a string with all letters converted to lowercase.")]
    [SeeAlso(typeof(ToUpperVariableFunction))]
    [Tag("strings")]
    public sealed class ToLowerVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The string to convert to lowercase.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(object context) => this.Text.ToLowerInvariant();
    }
}
