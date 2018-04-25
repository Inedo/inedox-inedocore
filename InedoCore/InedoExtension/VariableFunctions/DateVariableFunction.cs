﻿using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.VariableFunctions
{
    [ScriptAlias("Date")]
    [Description("Returns the current date and time of the local timezone in the specified .NET datetime format string, or ISO 8601 format (yyyy-MM-ddThh:mm:ss) if no format is specified.")]
    [SeeAlso(typeof(DateUtcVariableFunction))]
#if BuildMaster
    [BuildMaster.Extensibility.VariableFunctions.LegacyAlias("DATE")]
#endif
    [Example(@"
# format strings taken from https://msdn.microsoft.com/en-us/library/az4se3k1(v=vs.110).aspx
set $OtterScript = >>
Now: $Date
UTC Now: $DateUtc

Custom: $Date(hh:mm:ss.f)
RFC1123: $Date(r)
Sortable: $Date(s)
Short time: $Date(t)
>>;

set $Result = $Eval($OtterScript);

Log-Information $Result;
")]
    public sealed class DateVariableFunction : CommonScalarVariableFunction
    {
        [VariableFunctionParameter(0, Optional = true)]
        [DisplayName("format")]
        [Description("An optional .NET datetime format string.")]
        public string Format { get; set; }

        protected override object EvaluateScalar(object context)
        {
            if (string.IsNullOrEmpty(this.Format))
                return DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss");
            else
                return DateTime.Now.ToString(this.Format);
        }
    }
}
