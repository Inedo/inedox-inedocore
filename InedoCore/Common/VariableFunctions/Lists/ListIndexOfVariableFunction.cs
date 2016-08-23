using Inedo.Documentation;
using Inedo.ExecutionEngine;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System;

namespace Inedo.Extensions.VariableFunctions.Lists
{
    [ScriptAlias("ListIndexOf")]
    [Description("Finds the index of an item in a list.")]
    [Tag("lists")]
    public sealed class ListIndexOfVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("list")]
        [Description("The list.")]
        public IEnumerable<RuntimeValue> List { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("item")]
        [Description("The item.")]
        public RuntimeValue Item { get; set; }

        protected override object EvaluateScalar(object context) => this.List.ToList().FindIndex(value => RuntimeValue.Equals(value, this.Item));
    }
}
