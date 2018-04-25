using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Lists
{
    [ScriptAlias("ListIndexOf")]
    [Description("Finds the index of an item in a list.")]
    [Tag("lists")]
    public sealed class ListIndexOfVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("list")]
        [Description("The list.")]
        public IEnumerable<RuntimeValue> List { get; set; }

        [VariableFunctionParameter(1)]
        [DisplayName("item")]
        [Description("The item.")]
        public RuntimeValue Item { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => this.List.ToList().FindIndex(value => RuntimeValue.Equals(value, this.Item));
    }
}
