using System;
using System.Collections.Generic;
using UnityEngine;

public static class EquipmentStatFormatter
{
    // 포맷타입: Flat = +5, Percent = +5%, PerSecond = +0.2/s 
    private enum FormatType { Flat, Percent, PerSecond, Plain }

    private static readonly (string label, FormatType fmt, Func<Equipment, double> getter)[] _entries =
    {
        // ===== 주스탯 =====
        ("힘", FormatType.Flat, eq => eq.bonusStrength),
        ("민첩", FormatType.Flat, eq => eq.bonusAgility),
        ("지능", FormatType.Flat, eq => eq.bonusIntelligence),

        // Rate는 0.1 당 10% 라고 써있으니, *100 해서 %로 표시
        ("힘", FormatType.Percent, eq => eq.bonusStrengthRate),
        ("민첩", FormatType.Percent, eq => eq.bonusAgilityRate),
        ("지능", FormatType.Percent, eq => eq.bonusIntelligenceRate),

        // ===== 파생 =====
        ("최대체력", FormatType.Flat, eq => eq.baseMaxHp),
        ("공격력", FormatType.Flat, eq => eq.bonusAttackDamage),
        ("체력회복", FormatType.Plain, eq => eq.hpRecovery),
        ("마나회복", FormatType.Plain, eq => eq.mpRecovery),
        ("공격속도", FormatType.PerSecond, eq => eq.attackSpeed), // APS 보너스
        ("치명확률", FormatType.Plain, eq => eq.criticalProbability),
        ("치명피해", FormatType.Percent, eq => eq.criticalDamage),
        ("최대마나", FormatType.Flat, eq => eq.maxMp),
        ("사거리", FormatType.Plain, eq => eq.attackRange),
    };

    // ADDED: 장비 1개의 “표시용 라인 목록” 생성
    public static List<string> BuildSummaryLines(Equipment eq)
    {
        var result = new List<string>();
        if (eq == null) return result;

        foreach (var entry in _entries)
        {
            double v = entry.getter(eq);

            if (Math.Abs(v) < 0.00001) continue; // 0이면 스킵

            result.Add(FormatLine(entry.label, v, entry.fmt));
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
