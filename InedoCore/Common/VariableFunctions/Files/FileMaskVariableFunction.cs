using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
using Inedo.IO;

namespace Inedo.Extensions.VariableFunctions.Files
{
    [ScriptAlias("FileMask")]
    [Description("Returns a list of files matching the mask on the current server.")]
    [Tag("files")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
    public sealed class FileMaskVariableFunction : CommonVectorVariableFunction, IAsyncVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("includes")]
        public IEnumerable<string> Includes { get; set; }

        [VariableFunctionParameter(1, Optional = true)]
        [ScriptAlias("excludes")]
        public IEnumerable<string> Excludes { get; set; }

        protected override IEnumerable EvaluateVector(object context)
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
                throw new VariableFunctionException("Execution context is not available.");

            var fileOps = execContext.Agent.GetService<IFileOperationsExecuter>();
            var fileInfos = fileOps.GetFileSystemInfosAsync(execContext.WorkingDirectory, new MaskingContext(this.Includes, this.Excludes)).Result();
            return fileInfos.Select(fi => fi.FullName);
        }

        public async Task<RuntimeValue> EvaluateAsync(IVariableFunctionContext context)
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
                throw new VariableFunctionException("Execution context is not available.");

            var fileOps = await execContext.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
            var fileInfos = await fileOps.GetFileSystemInfosAsync(execContext.WorkingDirectory, new MaskingContext(this.Includes, this.Excludes)).ConfigureAwait(false);
            return new RuntimeValue(fileInfos.Select(fi => new RuntimeValue(fi.FullName)));
        }
    }
}
