using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions
{
    public abstract class CommonScalarVariableFunction : ScalarVariableFunction
    {
        protected CommonScalarVariableFunction()
        {
        }

        protected sealed override object EvaluateScalar(IVariableFunctionContext context) => this.EvaluateScalar(context);

        protected abstract object EvaluateScalar(object context);
    }
}
