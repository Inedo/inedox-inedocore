namespace Inedo.Extensions.VariableFunctions.Math;

#nullable enable

/// <summary>
/// Simple infix math expression parser.
/// </summary>
/// <remarks>
/// Evaluates simple mathematical expresisons using +, -, *, /, %, (, and ) operators.
/// This does no variable replacement and operates only on realized values (parsed as <see cref="double"/>).
/// </remarks>
internal sealed class MathExpressionParser
{
    private readonly Stack<double> output = new();
    private readonly Stack<char> operators = new();

    public static double Evaluate(ReadOnlySpan<char> s) => new MathExpressionParser().EvaluateInternal(s);

    private double EvaluateInternal(ReadOnlySpan<char> s)
    {
        var remaining = s.Trim();
        if (remaining.IsEmpty)
            throw new ExecutionFailureException("Cannot evaluate empty math expression.");

        do
        {
            if (TryExtractParenthesis(ref remaining, out var p))
                this.PushOperator(p);

            if (!TryExtractNumber(ref remaining, out var n))
                throw new ExecutionFailureException("Invalid expression.");

            this.PushValue(n);

            if (TryExtractOperator(ref remaining, out char op))
            {
                while (this.PushOperator(op))
                {
                    if (!TryExtractOperator(ref remaining, out op))
                        break;
                }
            }
            else if (!remaining.IsEmpty)
                throw new ExecutionFailureException("Invalid expression.");
        }
        while (!remaining.IsEmpty);

        this.DrainStack(false);

        return this.output.Pop();
    }
    private void DrainStack(bool untilParen)
    {
        while (this.operators.Count > 0)
        {
            var o = this.operators.Pop();
            if (o == '(')
            {
                if (untilParen)
                    return;
                else
                    continue;
            }

            this.EvaluateTopOperator(o);
        }
    }
    private bool PushOperator(char op)
    {
        if (op != '(')
        {
            if (op == ')')
            {
                this.DrainStack(true);
                return true;
            }
            else
            {
                while (this.operators.Count > 0 && GetPrecedence(this.operators.Peek()) > GetPrecedence(op))
                {
                    this.EvaluateTopOperator(this.operators.Pop());
                }
            }
        }

        this.operators.Push(op);
        return false;
    }
    private void EvaluateTopOperator(char o)
    {
        var v2 = this.output.Pop();
        var v1 = this.output.Pop();
        var v = o switch
        {
            '+' => v1 + v2,
            '-' => v1 - v2,
            '*' => v1 * v2,
            '/' => v1 / v2,
            '%' => v1 % v2,
            _ => throw new ArgumentException("Invalid operator.")
        };

        this.output.Push(v);
    }
    private void PushValue(double value)
    {
        this.output.Push(value);
    }

    private static bool TryExtractNumber(ref ReadOnlySpan<char> s, out double value)
    {
        value = 0;
        if (s.IsEmpty)
            return false;

        var numSpan = s;
        int start;

        if (char.IsNumber(s[0]))
            start = 1;
        else if (s.Length > 1 && s[0] == '-' && char.IsNumber(s[1]))
            start = 2;
        else
            return false;

        for (int i = start; i < s.Length; i++)
        {
            if (char.IsNumber(s[i]) || s[i] == '.' || s[i] == 'e' || (s[i - 1] == 'e' && s[i] == '-'))
                continue;

            numSpan = s[..i];
            break;
        }

        if (double.TryParse(numSpan, out value))
        {
            s = s[numSpan.Length..].TrimStart();
            return true;
        }

        return false;
    }
    private static bool TryExtractOperator(ref ReadOnlySpan<char> s, out char op)
    {
        op = default;

        if (s.IsEmpty)
            return false;

        if (s[0] is '+' or '-' or '*' or '/' or '%' or ')')
        {
            op = s[0];
            s = s[1..].TrimStart();
            return true;
        }

        return false;
    }
    private static bool TryExtractParenthesis(ref ReadOnlySpan<char> s, out char op)
    {
        op = default;
        if (s.IsEmpty)
            return false;

        if (s[0] is '(' or ')')
        {
            op = s[0];
            s = s[1..].TrimStart();
            return true;
        }

        return false;
    }
    private static int GetPrecedence(char op)
    {
        return op switch
        {
            '+' or '-' => 0,
            '*' or '/' or '%' => 1,
            '(' or ')' => -1,
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
    }
}
