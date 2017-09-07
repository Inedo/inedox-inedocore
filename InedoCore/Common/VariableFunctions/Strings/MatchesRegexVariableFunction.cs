using System.ComponentModel;
using System.Text.RegularExpressions;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("MatchesRegex")]
    [Description("Returns true when the specified text matches the specified pattern; otherwise returns false.")]
    [Tag("strings")]
    public sealed class MatchesRegexVariableFunction : CommonScalarVariableFunction
    {
        [DisplayName("text")]
        [Description("The text which will be evaluated by the regular expression.")]
        [VariableFunctionParameter(0)]
        public string Text { get; set; }
        [DisplayName("regex")]
        [Description("The regular expression pattern.")]
        [VariableFunctionParameter(1)]
        public string RegexPattern { get; set; }

        protected override object EvaluateScalar(object context) => Regex.IsMatch(this.Text, this.RegexPattern);
    }
}
