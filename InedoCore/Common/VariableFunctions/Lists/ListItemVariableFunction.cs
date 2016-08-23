using Inedo.Documentation;
using Inedo.ExecutionEngine;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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

#if BuildMaster
        public override RuntimeValue Evaluate(IGenericBuildMasterContext context)
#elif Otter
        public override RuntimeValue Evaluate(IOtterContext context)
#endif
        {
            return this.List.ElementAt(this.Index);
        }
    }
}
