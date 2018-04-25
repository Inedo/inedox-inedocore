using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("ToLower")]
    [Description("Returns a string with all letters converted to lowercase.")]
    [SeeAlso(typeof(ToUpperVariableFunction))]
    [Tag("strings")]
    public sealed class ToLowerVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The string to convert to lowercase.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => this.Text.ToLowerInvariant();
    }
}
