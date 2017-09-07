using System;
using System.Collections;
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
    [ScriptAlias("Split")]
    [Description("Splits a string into substrings based on a specified separator.")]
    [SeeAlso(typeof(JoinVariableFunction))]
    [Tag("strings")]
    public sealed class SplitVariableFunction : CommonVectorVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("String to split.")]
        public string Text { get; set; }

        [DisplayName("separator")]
        [VariableFunctionParameter(1)]
        [Description("String that delimits the substrings in this string.")]
        public string Separator { get; set; }

        [DisplayName("count")]
        [VariableFunctionParameter(2, Optional = true)]
        [Description("The maximum number of substrings to return. If not specified, all substrings are returned.")]
        public int? Count { get; set; }

        protected override IEnumerable EvaluateVector(object context)
        {
            if (this.Count < 0)
                throw new VariableFunctionArgumentException("Count cannot be negative.");

            if (this.Count == null)
                return this.Text.Split(new[] { this.Separator }, StringSplitOptions.None);
            else
                return this.Text.Split(new[] { this.Separator }, (int)this.Count, StringSplitOptions.None);
        }
    }
}
