using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class EquipmentStatFormatter
{
    // 필드명 -> (표시명, 포맷타입)
    // 포맷타입: Flat = +5, Percent = +5%, PerSecond = +0.2/s 
    private enum FormatType { Flat, Percent, PerSecond, Plain }

    private static readonly Dictionary<string, (string label, FormatType fmt)> _map = new()
    {
        // ===== 주스탯 =====
        { "bonusStrength", ("힘", FormatType.Flat) },
        { "bonusAgility", ("민첩", FormatType.Flat) },
        { "bonusIntelligence", ("지능", FormatType.Flat) },

        // Rate는 0.1 당 10% 라고 써있으니, *100 해서 %로 표시
        { "bonusStrengthRate", ("힘", FormatType.Percent) },
        { "bonusAgilityRate", ("민첩", FormatType.Percent) },
        { "bonusIntelligenceRate", ("지능", FormatType.Percent) },

        // ===== 파생 =====
        { "baseMaxHp", ("최대체력", FormatType.Flat) },
        { "bonusAttackDamage", ("공격력", FormatType.Flat) },
        { "hpRecovery", ("체력회복", FormatType.Plain) },
        { "mpRecovery", ("마나회복", FormatType.Plain) },

        { "attackSpeed", ("공격속도", FormatType.PerSecond) }, // APS 보너스
        { "criticalProbability", ("치명확률", FormatType.Plain) }, // 보통 %로 보여줌
        { "criticalDamage", ("치명피해", FormatType.Percent) },      // 0.2면 +20% 같은식

        { "maxMp", ("최대마나", FormatType.Flat) },
        { "attackRange", ("사거리", FormatType.Plain) },
    };

    // ADDED: 장비 1개의 “표시용 라인 목록” 생성
    public static List<string> BuildSummaryLines(Equipment eq)
    {
        var result = new List<string>();
        if (eq == null) return result;

        var t = typeof(Equipment);
        foreach (var kv in _map)
        {
            var field = t.GetField(kv.Key, BindingFlags.Instance | BindingFlags.Public);
            if (field == null) continue;

            object raw = field.GetValue(eq);
            double v = 0;

            // float/double/int 대응
            if (raw is int i) v = i;
            else if (raw is float f) v = f;
            else if (raw is double d) v = d;
            else continue;

            if (Math.Abs(v) < 0.00001) continue; // 0이면 스킵

            var (label, fmt) = kv.Value;
            result.Add(FormatLine(label, v, fmt));
        }

        return result;
    }

    private static string FormatLine(string label, double v, FormatType fmt)
    {
        // + / - 기호
        string sign = v >= 0 ? "+" : "";

        switch (fmt)
        {
            case FormatType.Flat:
                // 정수처럼 보이게
                return $"{label} {sign}{v:0}";
            case FormatType.Percent:
                // “Rate는 0.1 당 10%” / 확률류도 %로 보여주고 싶으면 통일해서 *100
                return $"{label} {sign}{v * 100:0.#}%";
            case FormatType.PerSecond:
                // 공격속도 APS 보너스: +0.1/s
                return $"{label} {sign}{v:0.##}/s";
            case FormatType.Plain:
            default:
                return $"{label} {sign}{v:0.##}";
        }
    }

    
    public static string BuildInlineSummary(Equipment eq)
    {
        var lines = BuildSummaryLines(eq);
        return (lines == null || lines.Count == 0) ? "" : string.Join(" / ", lines);
    }
}