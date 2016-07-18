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
    [ScriptAlias("Replace")]
    [Description("Searches for and replaces text in a string.")]
    [Tag("strings")]
    public sealed class ReplaceVariableFunction : CommonScalarVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The string to search for replacements.")]
        public string Text { get; set; }

        [DisplayName("value")]
        [VariableFunctionParameter(1)]
        [Description("The substring to search for in the first argument.")]
        public string Value { get; set; }

        [DisplayName("replaceWith")]
        [VariableFunctionParameter(2)]
        [Description("The substring to replace occurrences of the second parameter with.")]
        public string ReplaceWith { get; set; }

        protected override object EvaluateScalar(object context) => this.Text.Replace(this.Value, this.ReplaceWith);
    }
}
