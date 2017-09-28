using System;
using System.ComponentModel;
using System.IO;
using Inedo.Agents;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Files
{
    [ScriptAlias("FileContents")]
    [Description("Returns the contents of a file on the current server.")]
    [Tag("files")]
#if Hedgehog
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Hedgehog | InedoProduct.Otter)]
#endif
    public sealed class FileContentsVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("name")]
        [Description("The path of the file.")]
        public string FileName { get; set; }

        [VariableFunctionParameter(1, Optional = true)]
        [ScriptAlias("maxLength")]
        [Description("The maximum length (in characters) of the file to read.")]
        public int? MaxLength { get; set; }

        protected override object EvaluateScalar(object context)
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
                throw new VariableFunctionException("Execution context is not available.");

            var path = execContext.ResolvePath(this.FileName);

            var fileOps = execContext.Agent.GetService<IFileOperationsExecuter>();
            using (var file = fileOps.OpenFile(path, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(file))
            {
                if (this.MaxLength == null)
                {
                    return reader.ReadToEnd();
                }
                else
                {
                    int index = 0;
                    int length = (int)this.MaxLength;
                    var buffer = new char[length];
                    int read;
                    while ((read = reader.ReadBlock(buffer, index, length - index)) > 0)
                    {
                        index += read;
                    }

                    return new string(buffer, 0, index);
                }
            }
        }
    }
}
