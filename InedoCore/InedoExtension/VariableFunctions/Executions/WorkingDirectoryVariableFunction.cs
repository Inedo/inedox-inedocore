using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
using Inedo.IO;

namespace Inedo.Extensions.VariableFunctions.Executions
{
    [ScriptAlias("WorkingDirectory")]
    [ScriptAlias("CurrentDirectory")]
    [Description("Returns the current working directory.")]
    [Tag("executions")]
    public sealed class WorkingDirectoryVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            return PathEx.EnsureTrailingDirectorySeparator((context as IOperationExecutionContext)?.WorkingDirectory);
        }
    }
}
