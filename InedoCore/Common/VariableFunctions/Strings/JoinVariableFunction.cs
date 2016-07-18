using System.Collections.Generic;
using System.ComponentModel;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("Join")]
    [Description("Concatenates all elements of a list into a string using a specified separator.")]
    [SeeAlso(typeof(SplitVariableFunction))]
    [Tag("strings")]
    public sealed class JoinVariableFunction : CommonScalarVariableFunction
    {
        [DisplayName("separator")]
        [VariableFunctionParameter(0)]
        [Description("The string to use as a separator. The separator is only included if the list contains more than one element.")]
        public string Separator { get; set; }

        [DisplayName("values")]
        [VariableFunctionParameter(1)]
        [Description("The elements to concatenate.")]
        public IEnumerable<string> Values { get; set; }

        protected override object EvaluateScalar(object context) => string.Join(this.Separator, this.Values);
    }
}
