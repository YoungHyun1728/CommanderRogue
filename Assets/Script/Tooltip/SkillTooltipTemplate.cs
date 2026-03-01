using System.Text.RegularExpressions;
using UnityEngine;

public static class SkillTooltipTemplate
{
    // {i:EXPR}  -> int (floor)
    // {r:EXPR}  -> int (round)
    // {f1:EXPR} -> 1 decimal
    private static readonly Regex TokenRegex =
        new Regex(@"\{(?<fmt>i|r|f1):(?<expr>[^}]+)\}", RegexOptions.Compiled);

    public static string Render(string template, Unit u)
    {
        if (string.IsNullOrEmpty(template) || u == null) return template;

        return TokenRegex.Replace(template, m =>
        {
            string fmt = m.Groups["fmt"].Value;
            string expr = m.Groups["expr"].Value;

            double val = TooltipExpression.Evaluate(expr, v => ResolveVar(v, u));

            return fmt switch
            {
                "i" => Mathf.FloorToInt((float)val).ToString("N0"),
                "r" => Mathf.RoundToInt((float)val).ToString("N0"),
                "f1" => ((float)val).ToString("0.0"),
                _ => val.ToString("0.##")
            };
        });
    }

    private static double ResolveVar(string name, Unit u)
    {
        name = name.Trim().ToUpperInvariant();

        return name switch
        {
            "STR" => u.totalStrength,
            "AGI" => u.totalAgility,
            "INT" => u.totalIntelligence,
            "MAXHP" => u.maxHp,
            "MAXMP" => u.maxMp,
            "ATK" => u.attackDamage,
            "APS" => u.EffectiveAttackSpeed,
            _ => 0
        };
    }
}