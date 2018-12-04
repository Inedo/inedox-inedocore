using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("RegexFind")]
    [Description("Finds all matches of a regular expression in a string, optionally returning only a matched group.")]
    [Tag("strings")]
    public sealed class RegexFindVariableFunction : VectorVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The string to search for replacements.")]
        public string Text { get; set; }

        [DisplayName("matchExpression")]
        [VariableFunctionParameter(1)]
        [Description("The regular expression used to search the first argument.")]
        public string MatchExpression { get; set; }

        [DisplayName("matchGroup")]
        [VariableFunctionParameter(2, Optional = true)]
        [Description("When specified, the name or index of each match subexpression to return instead of the entire match.")]
        public string MatchGroup { get; set; }

        protected override IEnumerable EvaluateVector(IVariableFunctionContext context)
        {
            var results = new List<string>();

            int? groupIndex = AH.ParseInt(this.MatchGroup);

            foreach (Match m in Regex.Matches(this.Text ?? string.Empty, this.MatchExpression ?? string.Empty, RegexOptions.None, new TimeSpan(0, 0, 30)))
            {
                if (groupIndex.HasValue)
                {
                    var group = m.Groups[groupIndex.Value];
                    if (group.Success)
                        results.Add(group.Value);
                }
                else if (!string.IsNullOrEmpty(this.MatchGroup))
                {
                    var group = m.Groups[this.MatchGroup];
                    if (group.Success)
                        results.Add(group.Value);
                }
                else
                {
                    results.Add(m.Value);
                }
            }

            return results;
        }
    }
}
