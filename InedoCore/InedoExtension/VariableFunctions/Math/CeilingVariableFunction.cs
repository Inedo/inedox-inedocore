using Inedo.Extensibility.VariableFunctions;

#nullable enable

namespace Inedo.Extensions.VariableFunctions.Math;

[ScriptAlias("Ceiling")]
[Description("Returns the value rounded up to the nearest integer.")]
[SeeAlso(typeof(FloorVariableFunction))]
[Tag("math")]
public sealed class CeilingVariableFunction : ScalarVariableFunction
{
    [VariableFunctionParameter(0)]
    [ScriptAlias("value")]
    [Description("The value to round up.")]
    public double Value { get; set; }

    protected override object EvaluateScalar(IVariableFunctionContext context) => System.Math.Ceiling(this.Value);
}
