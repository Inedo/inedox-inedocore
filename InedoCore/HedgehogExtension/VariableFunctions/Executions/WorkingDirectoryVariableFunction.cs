using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    partial class WorkingDirectoryVariableFunction
    {
        protected override object EvaluateScalar(IStandardContext context)
        {
            return PathEx.EnsureTrailingDirectorySeparator((context as IOperationExecutionContext)?.WorkingDirectory);
        }
    }
}
