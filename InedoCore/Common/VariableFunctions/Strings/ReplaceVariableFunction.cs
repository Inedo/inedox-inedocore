using System.ComponentModel;
using Inedo.Documentation;
using System;
using System.Text.RegularExpressions;
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

        [DisplayName("ignoreCase")]
        [VariableFunctionParameter(3, Optional = true)]
        [Description("When \"true\", string comparison will be performed with case insensitivity; the default is \"false\".")]
        public bool IgnoreCase { get; set; }

        protected override object EvaluateScalar(object context)
        {
            if (this.IgnoreCase)
                return Regex.Replace(this.Text, Regex.Escape(this.Value), this.ReplaceWith, RegexOptions.IgnoreCase);
            else
                return this.Text.Replace(this.Value, this.ReplaceWith);
        }
    }
}
