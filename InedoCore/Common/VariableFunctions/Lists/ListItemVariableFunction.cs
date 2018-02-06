using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Lists
{
    [ScriptAlias("ListItem")]
    [Description("Gets an item from a list.")]
    [Tag("lists")]
    public sealed class ListItemVariableFunction : VariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("list")]
        [Description("The list.")]
        public IEnumerable<RuntimeValue> List { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("index")]
        [Description("The index of the item.")]
        public int Index { get; set; }

        public override RuntimeValue Evaluate(IVariableFunctionContext context)
        {
            return this.List.ElementAt(this.Index);
        }
    }
}
