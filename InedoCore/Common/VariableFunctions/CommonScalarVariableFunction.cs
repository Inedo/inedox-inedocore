#if Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions
{
    public abstract class CommonScalarVariableFunction : ScalarVariableFunction
    {
        protected CommonScalarVariableFunction()
        {
        }

#if Otter
        protected sealed override object EvaluateScalar(IOtterContext context) => this.EvaluateScalar(context);
#elif BuildMaster
        protected sealed override object EvaluateScalar(IGenericBuildMasterContext context) => this.EvaluateScalar(context);
#elif Hedgehog
        protected sealed override object EvaluateScalar(IVariableFunctionContext context) => this.EvaluateScalar(context);
#endif

        protected abstract object EvaluateScalar(object context);
    }
}
