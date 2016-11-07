using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    [ScriptAlias("TrimStart")]
    [Description("Returns a string with all leading whitespace characters removed, or optionally a set of specified characters.")]
    [VariadicVariableFunction(nameof(CharactersToTrim))]
    [Tag("strings")]
    public sealed class TrimStartVariableFunction : CommonScalarVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The input string.")]
        public string Text { get; set; }

        [Description("Characters to trim.")]
        public IEnumerable<string> CharactersToTrim { get; set; }

        protected override object EvaluateScalar(object context)
        {
            var chars = (from s in this.CharactersToTrim ?? new string[0]
                         where s.Length == 1
                         select s[0]).ToArray();

            if (chars.Length > 0)
                return this.Text.TrimStart(chars);
            else
                return this.Text.TrimStart();
        }
    }
}
