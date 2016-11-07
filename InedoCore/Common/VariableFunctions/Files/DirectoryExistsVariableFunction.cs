using System.ComponentModel;
using Inedo.Agents;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Files
{
    [ScriptAlias("DirectoryExists")]
    [Description("Returns \"true\" if the specified directory exists on the current server.")]
    [Tag("files")]
    public sealed class DirectoryExistsVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("name")]
        [Description("The path of the directory.")]
        public string DirectoryName { get; set; }

        protected override object EvaluateScalar(object context)
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
                throw new VariableFunctionException("Execution context is not available.");

            return execContext.Agent
                .GetService<IFileOperationsExecuter>()
                .DirectoryExists(execContext.ResolvePath(this.DirectoryName));
        }
    }
}
