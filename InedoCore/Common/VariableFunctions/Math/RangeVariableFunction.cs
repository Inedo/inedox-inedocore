using System.Collections;
using System.ComponentModel;
using System.Linq;
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

namespace Inedo.Extensions.VariableFunctions.Math
{
    [ScriptAlias("Range")]
    [Description("Returns a range of integers starting from a specified value.")]
    public sealed class RangeVariableFunction : CommonVectorVariableFunction
    {
        [DisplayName("start")]
        [Description("The first integer of the sequence.")]
        [VariableFunctionParameter(0)]
        public int Start { get; set; }

        [DisplayName("count")]
        [Description("The number of integers to return.")]
        [VariableFunctionParameter(1)]
        public int Count { get; set; }

        protected override IEnumerable EvaluateVector(object context)
        {
            if (this.Count < 0)
                throw new VariableFunctionArgumentException("Count cannot be negative.");

            return Enumerable.Range(this.Start, this.Count);
        }
    }
}
