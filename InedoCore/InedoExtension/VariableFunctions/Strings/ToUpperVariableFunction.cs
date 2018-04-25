using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("ToUpper")]
    [Description("Returns a string with all letters converted to uppercase.")]
    [SeeAlso(typeof(ToLowerVariableFunction))]
    [Tag("strings")]
    public sealed class ToUpperVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("text")]
        [Description("The string to convert to uppercase.")]
        public string Text { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => this.Text.ToUpperInvariant();
    }
}
