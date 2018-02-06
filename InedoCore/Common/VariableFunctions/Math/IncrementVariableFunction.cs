using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions
{
    [ScriptAlias("Increment")]
    [Description("Returns a string that contains the result of incrementing a value.")]
    [SeeAlso(typeof(DecrementVariableFunction))]
    [Tag("math")]
    public sealed class IncrementVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("value")]
        [Description("The value to increment.")]
        public decimal Value { get; set; }
        [VariableFunctionParameter(1, Optional = true)]
        [ScriptAlias("amount")]
        [Description("The amount that will be added to the value. If not specified, 1 is used.")]
        public decimal Amount { get; set; } = 1;

        protected override object EvaluateScalar(object context) => this.Value + this.Amount;
    }
}
