using Inedo.Extensibility.VariableFunctions;

#nullable enable

namespace Inedo.Extensions.VariableFunctions.Math;

[ScriptAlias("Abs")]
[Description("Returns the absolute value of a number.")]
[Tag("math")]
public sealed class AbsVariableFunction : ScalarVariableFunction
{
    [VariableFunctionParameter(0)]
    [ScriptAlias("value")]
    [Description("The value to return the absolute value for.")]
    public double Value { get; set; }

    protected override object EvaluateScalar(IVariableFunctionContext context) => System.Math.Abs(this.Value);
}
