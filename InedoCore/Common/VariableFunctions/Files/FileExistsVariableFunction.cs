using System.ComponentModel;
using Inedo.Agents;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Files
{
    [ScriptAlias("FileExists")]
    [Description("Returns \"true\" if the specified file exists on the current server.")]
    [Tag("files")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
    public sealed class FileExistsVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("name")]
        [Description("The path of the file.")]
        public string FileName { get; set; }

        protected override object EvaluateScalar(object context)
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
                throw new VariableFunctionException("Execution context is not available.");

            return execContext.Agent
                .GetService<IFileOperationsExecuter>()
                .FileExists(execContext.ResolvePath(this.FileName));
        }
    }
}
