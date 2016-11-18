using System.ComponentModel;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    [ScriptAlias("ExecutionId")]
    [Description("Returns the current execution ID.")]
    [Tag("executions")]
    public sealed class ExecutionIdVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IGenericBuildMasterContext context) => context.ExecutionId;
    }
}
