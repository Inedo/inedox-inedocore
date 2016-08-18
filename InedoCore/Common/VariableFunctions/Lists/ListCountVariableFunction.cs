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

namespace Inedo.Extensions.VariableFunctions.Lists
{
    [ScriptAlias("ListCount")]
    [Description("Count the number of elements in a list.")]
    [Tag("lists")]
    public sealed class ListCountVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [DisplayName("list")]
        [Description("The list.")]
        public IEnumerable<RuntimeValue> List { get; set; }

        protected override object EvaluateScalar(object context) => this.List.Count();
    }
}
