using System.ComponentModel;
using Inedo.Agents;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Files
{
    [ScriptAlias("DirectoryExists")]
    [Description("Returns \"true\" if the specified directory exists on the current server.")]
    [Tag("files")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class DirectoryExistsVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("name")]
        [Description("The path of the directory.")]
        public string DirectoryName { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
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
