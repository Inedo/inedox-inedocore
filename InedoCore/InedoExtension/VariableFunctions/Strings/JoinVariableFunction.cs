using System.Collections.Generic;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("Join")]
    [Description("Concatenates all elements of a list into a string using a specified separator.")]
    [SeeAlso(typeof(SplitVariableFunction))]
    [Tag("strings")]
    public sealed class JoinVariableFunction : ScalarVariableFunction
    {
        [DisplayName("separator")]
        [VariableFunctionParameter(0)]
        [Description("The string to use as a separator. The separator is only included if the list contains more than one element.")]
        public string Separator { get; set; }

        [DisplayName("values")]
        [VariableFunctionParameter(1)]
        [Description("The elements to concatenate.")]
        public IEnumerable<string> Values { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => string.Join(this.Separator, this.Values);
    }
}
