using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    partial class WorkingDirectoryVariableFunction
    {
        protected override object EvaluateScalar(IGenericBuildMasterContext context)
        {
            return PathEx.EnsureTrailingDirectorySeparator((context as IOperationExecutionContext)?.WorkingDirectory ?? (context as IAgentBasedActionExecutionContext)?.SourceDirectory);
        }
    }
}
