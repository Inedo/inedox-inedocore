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
    [ScriptAlias("DirectoryExists")]
    [Description("Returns \"true\" if the specified directory exists on the current server.")]
    [Tag("files")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class DirectoryExistsVariableFunction : ScalarVariableFunction, IAsyncVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("name")]
        [Description("The path of the directory.")]
        public string DirectoryName { get; set; }

        public async ValueTask<RuntimeValue> EvaluateAsync(IVariableFunctionContext context)
        {
            if (context is not IOperationExecutionContext execContext)
                throw new VariableFunctionException("Execution context is not available.");

            var fileOps = await execContext.Agent.GetServiceAsync<IFileOperationsExecuter>();
            return await fileOps.DirectoryExistsAsync(this.DirectoryName);
        }

        protected override object EvaluateScalar(IVariableFunctionContext context) => throw new NotImplementedException();
    }
}
