using System.ComponentModel;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("PadRight")]
    [Description("Returns a new string that left-aligns the characters in this instance by padding them on the left with a specified character, for a specified total length.")]
    [Tag("strings")]
    public sealed class PadRightVariableFunction : CommonScalarVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The input string.")]
        public string Text { get; set; }
        [DisplayName("length")]
        [VariableFunctionParameter(1)]
        [Description("The length of the string to return.")]
        public int Length { get; set; }
        [DisplayName("padCharacter")]
        [VariableFunctionParameter(2, Optional = true)]
        [Description("The character to be inserted as padding. The default is a space.")]
        public string PadCharacter { get; set; }

        protected override object EvaluateScalar(object context)
        {
            char padCharacter = string.IsNullOrEmpty(this.PadCharacter) ? ' ' : this.PadCharacter[0];
            return (this.Text ?? string.Empty).PadRight(this.Length, padCharacter);
        }
    }
}
