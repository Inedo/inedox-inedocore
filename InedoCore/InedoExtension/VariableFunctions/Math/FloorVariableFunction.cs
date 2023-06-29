using Inedo.Extensibility.VariableFunctions;

#nullable enable

namespace Inedo.Extensions.VariableFunctions.Math;

[ScriptAlias("Floor")]
[Description("Returns the value rounded down to the nearest integer.")]
[SeeAlso(typeof(CeilingVariableFunction))]
[Tag("math")]
public sealed class FloorVariableFunction : ScalarVariableFunction
{
    [VariableFunctionParameter(0)]
    [ScriptAlias("value")]
    [Description("The value to round down.")]
    public double Value { get; set; }

    protected override object EvaluateScalar(IVariableFunctionContext context) => System.Math.Floor(this.Value);
}
