using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("RegexReplace")]
    [Description("Searches for and replaces text in a string using a regular expression.")]
    [Tag("strings")]
    public sealed class RegexReplaceVariableFunction : ScalarVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The string to search for replacements.")]
        public string Text { get; set; }

        [DisplayName("matchExpression")]
        [VariableFunctionParameter(1)]
        [Description("The regular expression used to search the first argument.")]
        public string MatchExpression { get; set; }

        [DisplayName("replaceWith")]
        [VariableFunctionParameter(2)]
        [Description("The substring to replace occurrences of the second parameter with.")]
        public string ReplaceWith { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            try
            {
                return Regex.Replace(this.Text ?? string.Empty, this.MatchExpression ?? string.Empty, this.ReplaceWith ?? string.Empty, RegexOptions.None, new TimeSpan(0, 0, 30));
            }
            catch (ArgumentException ex)
            {
                throw new ExecutionFailureException($"Error evaluating regex \"{this.MatchExpression}\": {ex.Message}");
            }
        }
    }
}
