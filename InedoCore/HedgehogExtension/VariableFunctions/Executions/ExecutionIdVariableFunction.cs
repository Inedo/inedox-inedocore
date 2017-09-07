using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    [ScriptAlias("ExecutionId")]
    [Description("Returns the current execution ID.")]
    [Tag("executions")]
    public sealed class ExecutionIdVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IStandardContext context) => context.ExecutionId;
    }
}
