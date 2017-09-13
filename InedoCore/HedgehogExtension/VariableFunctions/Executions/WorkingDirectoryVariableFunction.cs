using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
using Inedo.IO;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    partial class WorkingDirectoryVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            return PathEx.EnsureTrailingDirectorySeparator((context as IOperationExecutionContext)?.WorkingDirectory);
        }
    }
}
