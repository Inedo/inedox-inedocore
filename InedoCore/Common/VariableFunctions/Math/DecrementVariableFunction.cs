using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions
{
    [ScriptAlias("Decrement")]
    [Description("Returns a string that contains the result of decrementing a value.")]
    [SeeAlso(typeof(IncrementVariableFunction))]
    [Tag("math")]
    public sealed class DecrementVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("value")]
        [Description("The value to decrement.")]
        public decimal Value { get; set; }
        [VariableFunctionParameter(1, Optional = true)]
        [ScriptAlias("amount")]
        [Description("The amount that will be subtracted from the value. If not specified, 1 is used.")]
        public decimal Amount { get; set; } = 1;

        protected override object EvaluateScalar(object context) => this.Value - this.Amount;
    }
}
