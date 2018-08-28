using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions
{
    [ScriptAlias("Compare")]
    [Description("Compare two values numerically or as case-sensitive strings.")]
    [Tag("math")]
    [Tag("strings")]
    public sealed class CompareVariableFunction : ScalarVariableFunction
    {
        private static decimal? TryParseDecimal(string s) => decimal.TryParse(s, out var num) ? num : (decimal?)null;

        [DisplayName("arg1")]
        [VariableFunctionParameter(0)]
        [Description("The left-hand side of the comparison.")]
        public string Arg1 { get; set; }

        [DisplayName("operator")]
        [VariableFunctionParameter(1)]
        [Description("One of: <, >, <=, >=, =, !=")]
        public string Operator { get; set; }

        [DisplayName("arg2")]
        [VariableFunctionParameter(2)]
        [Description("The right-hand side of the comparison.")]
        public string Arg2 { get; set; }

        [DisplayName("asNumber")]
        [VariableFunctionParameter(3, Optional = true)]
        [Description("Override number detection. True causes an error if the arguments cannot be parsed as numbers. False always compares them as strings.")]
        [DefaultValue((object)null)]
        public bool? AsNumber { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            if (string.IsNullOrEmpty(this.Operator))
                throw new VariableFunctionArgumentMissingException(nameof(this.Operator));
            if (this.Arg1 == null)
                this.Arg1 = string.Empty;
            if (this.Arg2 == null)
                this.Arg2 = string.Empty;

            decimal? num1 = null;
            decimal? num2 = null;
            if (this.AsNumber != false)
            {
                num1 = TryParseDecimal(this.Arg1);
                num2 = TryParseDecimal(this.Arg2);
            }

            if (this.AsNumber == true)
            {
                if (!num1.HasValue)
                    throw new VariableFunctionArgumentException("Invalid number: " + this.Arg1, nameof(this.Arg1));
                if (!num2.HasValue)
                    throw new VariableFunctionArgumentException("Invalid number: " + this.Arg2, nameof(this.Arg2));
            }

            int cmp;
            if (num1.HasValue && num2.HasValue)
                cmp = decimal.Compare(num1.Value, num2.Value);
            else
                cmp = string.Compare(this.Arg1, this.Arg2);

            switch (this.Operator)
            {
                case "<":
                    return cmp < 0;
                case ">":
                    return cmp > 0;
                case "<=":
                    return cmp <= 0;
                case ">=":
                    return cmp >= 0;
                case "=":
                    return cmp == 0;
                case "!=":
                    return cmp != 0;
                default:
                    throw new VariableFunctionArgumentException("Invalid operator: " + this.Operator, nameof(this.Operator));
            }
        }
    }
}
