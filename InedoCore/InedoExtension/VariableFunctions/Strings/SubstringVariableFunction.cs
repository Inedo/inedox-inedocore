using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("Substring")]
    [Description("Returns a segment of another string.")]
    [Tag("strings")]
    public sealed class SubstringVariableFunction : ScalarVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The input string.")]
        public string Text { get; set; }

        [DisplayName("offset")]
        [VariableFunctionParameter(1)]
        [Description("The zero-based offset into the first parameter that will begin the segment.")]
        public int Offset { get; set; }

        [DisplayName("length")]
        [VariableFunctionParameter(2, Optional = true)]
        [Description("The number of characters in the segment. If not specified, the remainder of the string is used.")]
        public int? Length { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            if (this.Offset < 0)
                throw new VariableFunctionArgumentException("Offset cannot be negative.");
            if (this.Length < 0)
                throw new VariableFunctionArgumentException("Length cannot be negative.");

            if (this.Offset >= this.Text.Length)
                return string.Empty;

            if (this.Length == null || this.Length >= this.Text.Length - this.Offset)
                return this.Text.Substring(this.Offset);

            return this.Text.Substring(this.Offset, (int)this.Length);
        }
    }
}
