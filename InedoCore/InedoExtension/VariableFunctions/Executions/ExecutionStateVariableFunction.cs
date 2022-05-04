using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    [ScriptAlias("ExecutionState")]
    [Description("Returns the current state of the execution (normal, warning, or error).")]
    [Tag("executions")]
    public sealed class ExecutionStateVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            if (context is not IOperationExecutionContext execContext)
                throw new VariableFunctionException("Execution context is not available.");

            if (execContext.ExecutionStatus == ExecutionStatus.Fault)
                return "error";

            return execContext.ExecutionStatus.ToString().ToLowerInvariant();
        }
    }
}
