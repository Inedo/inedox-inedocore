using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.IO;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("PathCombine")]
    [Description("Returns a string containing all of the arguments combined into a complete path.")]
    [VariadicVariableFunction(nameof(AdditionalPaths))]
    [Tag("files")]
    public sealed class PathCombineVariableFunction : CommonScalarVariableFunction
    {
        [DisplayName("path1")]
        [VariableFunctionParameter(0)]
        [Description("The first path element.")]
        public string Path1 { get; set; }

        [DisplayName("path2")]
        [VariableFunctionParameter(1)]
        [Description("The second path element.")]
        public string Path2 { get; set; }

        [Description("Additional path elements.")]
        public IEnumerable<string> AdditionalPaths { get; set; }

        protected override object EvaluateScalar(object context)
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext != null)
            {
                var fileOps = execContext.Agent.GetService<IFileOperationsExecuter>();

                if (this.AdditionalPaths == null)
                    return fileOps.CombinePath(this.Path1, this.Path2);
                else
                    return fileOps.CombinePath(new[] { this.Path1, this.Path2 }.Concat(this.AdditionalPaths).ToArray());
            }
            else
            {
                if (this.AdditionalPaths == null)
                    return PathEx.Combine(this.Path1, this.Path2);
                else
                    return PathEx.Combine(new[] { this.Path1, this.Path2 }.Concat(this.AdditionalPaths).ToArray());
            }
        }
    }
}
