using System.ComponentModel;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

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
