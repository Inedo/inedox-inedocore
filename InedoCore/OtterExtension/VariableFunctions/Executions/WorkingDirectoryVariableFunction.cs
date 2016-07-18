using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    partial class WorkingDirectoryVariableFunction
    {
        protected override object EvaluateScalar(IOtterContext context) => (context as IOperationExecutionContext)?.WorkingDirectory;
    }
}
