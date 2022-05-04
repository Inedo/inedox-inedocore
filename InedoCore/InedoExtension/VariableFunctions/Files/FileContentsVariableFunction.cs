using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Files
{
    [ScriptAlias("FileContents")]
    [Description("Returns the contents of a file on the current server.")]
    [Tag("files")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    public sealed class FileContentsVariableFunction : ScalarVariableFunction, IAsyncVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("name")]
        [Description("The path of the file.")]
        public string FileName { get; set; }

        [VariableFunctionParameter(1, Optional = true)]
        [ScriptAlias("maxLength")]
        [Description("The maximum length (in characters) of the file to read.")]
        public int? MaxLength { get; set; }

        public async ValueTask<RuntimeValue> EvaluateAsync(IVariableFunctionContext context)
        {
            if (context is not IOperationExecutionContext execContext)
                throw new VariableFunctionException("Execution context is not available.");

            var path = execContext.ResolvePath(this.FileName);

            var fileOps = await execContext.Agent.GetServiceAsync<IFileOperationsExecuter>();

            using var file = await fileOps.OpenFileAsync(path, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(file);
            if (this.MaxLength == null)
            {
                return await reader.ReadToEndAsync();
            }
            else
            {
                int length = this.MaxLength.GetValueOrDefault();
                var buffer = ArrayPool<char>.Shared.Rent(length);
                try
                {
                    var remaining = buffer.AsMemory(0, length);

                    int read;
                    while (!remaining.IsEmpty && (read = await reader.ReadBlockAsync(remaining, context.CancellationToken)) > 0)
                    {
                        remaining = remaining[read..];
                    }

                    return new string(buffer.AsSpan(0, length));
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
        }

        protected override object EvaluateScalar(IVariableFunctionContext context) => throw new NotImplementedException();
    }
}
