using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Files
{
    [ScriptAlias("FileExists")]
    [Description("Returns \"true\" if the specified file exists on the current server.")]
    [Tag("files")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class FileExistsVariableFunction : ScalarVariableFunction, IAsyncVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("name")]
        [Description("The path of the file.")]
        public string FileName { get; set; }

        public async ValueTask<RuntimeValue> EvaluateAsync(IVariableFunctionContext context)
        {
            if (context is not IOperationExecutionContext execContext)
                throw new VariableFunctionException("Execution context is not available.");

            return await (await execContext.Agent.GetServiceAsync<IFileOperationsExecuter>())
                .FileExistsAsync(execContext.ResolvePath(this.FileName));
        }

        protected override object EvaluateScalar(IVariableFunctionContext context) => throw new NotImplementedException();
    }
}
