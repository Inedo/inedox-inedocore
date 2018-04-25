using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("Trim")]
    [Description("Returns a string with all leading and trailing whitespace characters removed, or optionally a set of specified characters.")]
    [VariadicVariableFunction(nameof(CharactersToTrim))]
    [Tag("strings")]
    public sealed class TrimVariableFunction : ScalarVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The input string.")]
        public string Text { get; set; }

        [Description("Characters to trim.")]
        public IEnumerable<string> CharactersToTrim { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            var chars = (from s in this.CharactersToTrim ?? new string[0]
                         where s.Length == 1
                         select s[0]).ToArray();

            if (chars.Length > 0)
                return this.Text.Trim(chars);
            else
                return this.Text.Trim();
        }
    }
}
