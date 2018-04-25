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
    [ScriptAlias("ListRemove")]
    [Description("Removes an item from a list.")]
    [Tag("lists")]
    public sealed class ListRemoveVariableFunction : VectorVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("list")]
        [Description("The list.")]
        public IEnumerable<RuntimeValue> List { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("index")]
        [Description("The index of the item to remove.")]
        public int Index { get; set; }

        protected override IEnumerable EvaluateVector(IVariableFunctionContext context) => this.List.Take(this.Index).Concat(this.List.Skip(this.Index + 1));
    }
}
