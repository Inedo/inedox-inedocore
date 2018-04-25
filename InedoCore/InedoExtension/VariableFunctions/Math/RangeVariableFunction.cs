using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Math
{
    [ScriptAlias("Range")]
    [Description("Returns a range of integers starting from a specified value.")]
    public sealed class RangeVariableFunction : VectorVariableFunction
    {
        [DisplayName("start")]
        [Description("The first integer of the sequence.")]
        [VariableFunctionParameter(0)]
        public int Start { get; set; }

        [DisplayName("count")]
        [Description("The number of integers to return.")]
        [VariableFunctionParameter(1)]
        public int Count { get; set; }

        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            if (this.Count < 0)
                throw new VariableFunctionArgumentException("Count cannot be negative.");

            return Enumerable.Range(this.Start, this.Count);
        }
    }
}
