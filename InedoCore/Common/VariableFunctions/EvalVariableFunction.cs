﻿using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
#if Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
#endif

namespace Inedo.Extensions.VariableFunctions
{
    [ScriptAlias("Eval")]
    [Description("Performs variable substitution and function evaluation for arbitrary text.")]
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
    public sealed class EvalVariableFunction : VariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The text to process.")]
        public string Text { get; set; }

#if Otter
        public override RuntimeValue Evaluate(IOtterContext context)
#elif BuildMaster
        public override RuntimeValue Evaluate(IGenericBuildMasterContext context)
#elif Hedgehog
        public override RuntimeValue Evaluate(IVariableFunctionContext context)
#endif
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
                throw new NotSupportedException("This function can currently only be used within an execution.");

            return execContext.ExpandVariables(this.Text);
        }
    }
}
