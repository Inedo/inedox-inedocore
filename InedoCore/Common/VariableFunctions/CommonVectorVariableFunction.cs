using System.Collections;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions
{
    public abstract class CommonVectorVariableFunction : VectorVariableFunction
    {
        protected CommonVectorVariableFunction()
        {
        }

        protected sealed override IEnumerable EvaluateVector(IVariableFunctionContext context) => this.EvaluateVector(context);

        protected abstract IEnumerable EvaluateVector(object context);
    }
}
