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
    public sealed class ExecutionStateVariableFunction : CommonScalarVariableFunction
    {
        protected override object EvaluateScalar(object context)
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
                throw new VariableFunctionException("Execution context is not available.");

            if (execContext.ExecutionStatus == ExecutionStatus.Fault)
                return "error";

            return execContext.ExecutionStatus.ToString().ToLowerInvariant();
        }
    }
}
