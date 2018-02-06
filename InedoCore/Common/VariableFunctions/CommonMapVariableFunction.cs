using System.Collections.Generic;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions
{
    public abstract class CommonMapVariableFunction : VariableFunction
    {
        protected CommonMapVariableFunction()
        {
        }

        public sealed override RuntimeValue Evaluate(IVariableFunctionContext context) => new RuntimeValue(this.EvaluateMap(context));

        protected abstract IDictionary<string, RuntimeValue> EvaluateMap(object context);
    }
}
