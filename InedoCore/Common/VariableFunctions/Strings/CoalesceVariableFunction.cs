using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

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
