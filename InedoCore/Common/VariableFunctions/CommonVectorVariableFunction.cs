using System.Collections;
#if Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions
{
    public abstract class CommonVectorVariableFunction : VectorVariableFunction
    {
        protected CommonVectorVariableFunction()
        {
        }

#if Otter
        protected sealed override IEnumerable EvaluateVector(IOtterContext context) => this.EvaluateVector(context);
#elif BuildMaster
        protected sealed override IEnumerable EvaluateVector(IGenericBuildMasterContext context) => this.EvaluateVector(context);
#elif Hedgehog
        protected sealed override IEnumerable EvaluateVector(IHedgehogContext context) => this.EvaluateVector(context);
#endif

        protected abstract IEnumerable EvaluateVector(object context);
    }
}
