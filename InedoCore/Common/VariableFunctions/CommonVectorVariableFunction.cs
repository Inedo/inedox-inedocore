using System.Collections;
#if Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility.VariableFunctions;
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
#endif

        protected abstract IEnumerable EvaluateVector(object context);
    }
}
