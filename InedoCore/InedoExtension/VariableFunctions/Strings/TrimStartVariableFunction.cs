﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions.Strings
{
    [ScriptAlias("TrimStart")]
    [Description("Returns a string with all leading whitespace characters removed, or optionally a set of specified characters.")]
    [VariadicVariableFunction(nameof(CharactersToTrim))]
    [Tag("strings")]
    public sealed class TrimStartVariableFunction : ScalarVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The input string.")]
        public string Text { get; set; }

        [Description("Characters to trim.")]
        public IEnumerable<string> CharactersToTrim { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            var chars = (from s in this.CharactersToTrim ?? Array.Empty<string>()
                         where s.Length == 1
                         select s[0]).ToArray();

            if (chars.Length > 0)
                return this.Text.TrimStart(chars);
            else
                return this.Text.TrimStart();
        }
    }
}
