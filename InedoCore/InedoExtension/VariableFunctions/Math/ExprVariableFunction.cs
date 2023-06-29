using Inedo.Extensibility.VariableFunctions;

#nullable enable

namespace Inedo.Extensions.VariableFunctions.Math;

[Tag("math")]
[ScriptAlias("Expr")]
[Description("Evaluates a mathematical expression using +, -, *, /, or % operators.")]
public sealed class ExprVariableFunction : ScalarVariableFunction
{
    [VariableFunctionParameter(0)]
    [ScriptAlias("expression")]
    [Description("The expression to evaluate.")]
    public string? Expression { get; set; }

    protected override object EvaluateScalar(IVariableFunctionContext context)
    {
        if (string.IsNullOrWhiteSpace(this.Expression))
            throw new ExecutionFailureException("Cannot evaluate empty expression.");

        return MathExpressionParser.Evaluate(this.Expression);
    }
}
