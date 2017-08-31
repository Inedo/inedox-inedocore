using Inedo.Hedgehog;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    partial class WorkingDirectoryVariableFunction
    {
        protected override object EvaluateScalar(IHedgehogContext context)
        {
            return PathEx.EnsureTrailingDirectorySeparator((context as IOperationExecutionContext)?.WorkingDirectory);

        }
    }
}
