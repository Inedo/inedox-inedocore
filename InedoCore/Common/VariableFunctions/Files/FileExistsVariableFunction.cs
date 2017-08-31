using System.ComponentModel;
using Inedo.Agents;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Data;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.Operations;
using Inedo.Hedgehog.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Files
{
    [ScriptAlias("FileExists")]
    [Description("Returns \"true\" if the specified file exists on the current server.")]
    [Tag("files")]
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
