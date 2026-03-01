using System;
using System.Collections.Generic;
using System.Globalization;

public static class TooltipExpression
{
    // Supports: numbers, + - * /, parentheses, unary minus, variables [A-Z_]+
    public static double Evaluate(string expr, Func<string, double> varResolver)
    {
        if (string.IsNullOrWhiteSpace(expr)) return 0;

        var tokens = Tokenize(expr);
        var rpn = ToRpn(tokens);
        return EvalRpn(rpn, varResolver);
    }

    private enum TokType { Number, Ident, Op, LParen, RParen }

    private readonly struct Tok
    {
        public readonly TokType Type;
        public readonly string Text;
        public readonly double Number;

        public Tok(TokType type, string text, double number = 0)
        {
            Type = type; Text = text; Number = number;
        }
    }

    private static List<Tok> Tokenize(string s)
    {
        var tokens = new List<Tok>();
        int i = 0;

        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '(') { tokens.Add(new Tok(TokType.LParen, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new Tok(TokType.RParen, ")")); i++; continue; }

            if (c == '+' || c == '-' || c == '*' || c == '/')
            {
                tokens.Add(new Tok(TokType.Op, c.ToString()));
                i++;
                continue;
            }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                i++;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;

                var numStr = s.Substring(start, i - start);
                if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                    val = 0;

                tokens.Add(new Tok(TokType.Number, numStr, val));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                i++;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                var id = s.Substring(start, i - start);
                tokens.Add(new Tok(TokType.Ident, id));
                continue;
            }

            i++; // skip unknown
        }

        return FixUnaryMinus(tokens);
    }

    private static List<Tok> FixUnaryMinus(List<Tok> tokens)
    {
        var result = new List<Tok>(tokens.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Type == TokType.Op && t.Text == "-")
            {
                bool isUnary =
                    i == 0 ||
                    tokens[i - 1].Type == TokType.Op ||
                    tokens[i - 1].Type == TokType.LParen;

                if (isUnary)
                {
                    result.Add(new Tok(TokType.Op, "u-"));
                    continue;
                }
            }
            result.Add(t);
        }
        return result;
    }

    private static int Prec(string op) => op switch
    {
        "u-" => 3,
        "*" or "/" => 2,
        "+" or "-" => 1,
        _ => 0
    };

    private static bool RightAssoc(string op) => op == "u-";

    private static List<Tok> ToRpn(List<Tok> tokens)
    {
        var output = new List<Tok>();
        var ops = new Stack<Tok>();

        foreach (var t in tokens)
        {
            switch (t.Type)
            {
                case TokType.Number:
                case TokType.Ident:
                    output.Add(t);
                    break;

                case TokType.Op:
                {
                    while (ops.Count > 0 && ops.Peek().Type == TokType.Op)
                    {
                        var top = ops.Peek().Text;
                        if ((RightAssoc(t.Text) && Prec(t.Text) < Prec(top)) ||
                            (!RightAssoc(t.Text) && Prec(t.Text) <= Prec(top)))
                            output.Add(ops.Pop());
                        else break;
                    }
                    ops.Push(t);
                    break;
                }

                case TokType.LParen:
                    ops.Push(t);
                    break;

                case TokType.RParen:
                {
                    while (ops.Count > 0 && ops.Peek().Type != TokType.LParen)
                        output.Add(ops.Pop());
                    if (ops.Count > 0 && ops.Peek().Type == TokType.LParen)
                        ops.Pop();
                    break;
                }
            }
        }

        while (ops.Count > 0)
            output.Add(ops.Pop());

        return output;
    }

    private static double EvalRpn(List<Tok> rpn, Func<string, double> varResolver)
    {
        var st = new Stack<double>();

        foreach (var t in rpn)
        {
            if (t.Type == TokType.Number) { st.Push(t.Number); continue; }

            if (t.Type == TokType.Ident)
            {
                st.Push(varResolver?.Invoke(t.Text) ?? 0);
                continue;
            }

            if (t.Type == TokType.Op)
            {
                if (t.Text == "u-")
                {
                    var a = st.Count > 0 ? st.Pop() : 0;
                    st.Push(-a);
                    continue;
                }

                var b = st.Count > 0 ? st.Pop() : 0;
                var a2 = st.Count > 0 ? st.Pop() : 0;

                st.Push(t.Text switch
                {
                    "+" => a2 + b,
                    "-" => a2 - b,
                    "*" => a2 * b,
                    "/" => b == 0 ? 0 : a2 / b,
                    _ => 0
                });
            }
        }

        return st.Count > 0 ? st.Pop() : 0;
    }
}