using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Lists
{
    [ScriptAlias("ListCount")]
    [Description("Count the number of elements in a list.")]
    [Tag("lists")]
    public sealed class ListCountVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("list")]
        [Description("The list.")]
        public IEnumerable<RuntimeValue> List { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => this.List.Count();
    }
}
