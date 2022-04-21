using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;

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
    public sealed class EvalVariableFunction : VariableFunction, IAsyncVariableFunction
    {
        [DisplayName("text")]
        [VariableFunctionParameter(0)]
        [Description("The text to process.")]
        public string Text { get; set; }

        public override RuntimeValue Evaluate(IVariableFunctionContext context) => throw new NotSupportedException();
        public ValueTask<RuntimeValue> EvaluateAsync(IVariableFunctionContext context)
        {
            if (context is not IOperationExecutionContext execContext)
                throw new NotSupportedException("This function can only be used within an execution.");

            return execContext.ExpandVariablesAsync(this.Text);
        }
    }
}
