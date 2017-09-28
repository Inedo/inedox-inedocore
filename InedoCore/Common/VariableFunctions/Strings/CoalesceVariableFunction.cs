using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("Coalesce")]
    [VariadicVariableFunction(nameof(Arguments))]
    [Description("Returns the first argument which does not contain only whitespace.")]
    [Tag("strings")]
    public sealed class CoalesceVariableFunction : CommonScalarVariableFunction
    {
        [Description("Arguments to coalesce.")]
        public IEnumerable<string> Arguments { get; set; }

        protected override object EvaluateScalar(object context)
        {
            foreach (var arg in this.Arguments ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(arg))
                    return arg;
            }

            return string.Empty;
        }
    }
}
