using Inedo.Documentation;
using Inedo.ExecutionEngine;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Hedgehog;
using Inedo.Hedgehog.Data;
using Inedo.Hedgehog.Extensibility;
using Inedo.Hedgehog.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections;

namespace Inedo.Extensions.VariableFunctions.Lists
{
    [ScriptAlias("ListRemove")]
    [Description("Removes an item from a list.")]
    [Tag("lists")]
    public sealed class ListRemoveVariableFunction : CommonVectorVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("list")]
        [Description("The list.")]
        public IEnumerable<RuntimeValue> List { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("index")]
        [Description("The index of the item to remove.")]
        public int Index { get; set; }

        protected override IEnumerable EvaluateVector(object context) => this.List.Take(this.Index).Concat(this.List.Skip(this.Index + 1));
    }
}
