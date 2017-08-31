using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    [ScriptAlias("ExecutionId")]
    [Description("Returns the current execution ID.")]
    [Tag("executions")]
    public sealed class ExecutionIdVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IHedgehogContext context) => context.ExecutionId;
    }
}
