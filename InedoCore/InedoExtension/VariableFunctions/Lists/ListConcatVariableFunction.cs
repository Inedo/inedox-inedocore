using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Lists
{
    [ScriptAlias("ListConcat")]
    [Description("Creates a list containing the contents of each list in sequence.")]
    [Tag("lists")]
    [VariadicVariableFunction(nameof(Lists))]
    public sealed class ListConcatVariableFunction : VectorVariableFunction
    {
        [Description("The lists to concatenate.")]
        public IEnumerable<IEnumerable<RuntimeValue>> Lists { get; set; }

        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            return this.Lists?.SelectMany(l => l) ?? Enumerable.Empty<RuntimeValue>();
        }
    }
}
