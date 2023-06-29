using Inedo.Extensions.VariableFunctions.Math;

namespace InedoCoreTests;

[TestClass]
public sealed class ExpressionParserTests
{
    [DataTestMethod]
    [DataRow("2*(2+5)", 2 * (2 + 5))]
    [DataRow("1000/2+1", 1000 / 2 + 1)]
    [DataRow("(1+4)*2", (1 + 4) * 2)]
    [DataRow("1+4*2", 1 + 4 * 2)]
    [DataRow("1/2", 1.0 / 2)]
    [DataRow("2*(1+(7-3))", 2 * (1 + (7 - 3)))]
    [DataRow("-10+1", -10 + 1)]
    [DataRow("-10-1", -10 - 1)]
    [DataRow("50.7%12", 50.7 % 12)]
    [DataRow("1e10*2", 1e10 * 2)]
    [DataRow("1e-10*2", 1e-10 * 2)]
    public void TestEvaluate(string expr, double value)
    {
        Assert.AreEqual(value, MathExpressionParser.Evaluate(expr));
    }
}