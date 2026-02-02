using System.Collections.Generic;
using UnityEngine;

public class UnitStatusEffectController : MonoBehaviour
{
    private Unit unit;

    private class BuffInstance
    {
        public BuffDefinition def;
        public float endTime;

        // 되돌릴 값(델타) 저장
        public double addStr, addAgi, addInt;
        public double addStrRate, addAgiRate, addIntRate;
        public float attackIntervalMul;
    }

    private readonly List<BuffInstance> buffs = new();

    // "단일" 시간제 쉴드
    private float shieldEndTime = -1f;

    void Awake()
    {
        unit = GetComponent<Unit>();
    }

    void Update()
    {
        float now = Time.time;

        // Buff expire
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            if (now >= buffs[i].endTime)
            {
                RemoveBuffInternal(buffs[i]);
                buffs.RemoveAt(i);
            }
        }

        // Shield expire
        if (shieldEndTime > 0f && now >= shieldEndTime)
        {
            // 남아있든 말든 사라짐
            unit.shield = 0;
            unit.maxShield = 0;
            shieldEndTime = -1f;
        }
    }

    public void ApplyBuff(BuffDefinition def)
    {
        if (def == null || unit == null) return;

        var inst = new BuffInstance
        {
            def = def,
            endTime = Time.time + Mathf.Max(0.01f, def.duration),

            addStr = def.addStrength,
            addAgi = def.addAgility,
            addInt = def.addIntelligence,
            addStrRate = def.addStrengthRate,
            addAgiRate = def.addAgilityRate,
            addIntRate = def.addIntelligenceRate,
            attackIntervalMul = def.attackIntervalMultiplier
        };

        // 적용
        unit.bonusStrength += inst.addStr;
        unit.bonusAgility += inst.addAgi;
        unit.bonusIntelligence += inst.addInt;

        unit.bonusStrengthRate += inst.addStrRate;
        unit.bonusAgilityRate += inst.addAgiRate;
        unit.bonusIntelligenceRate += inst.addIntRate;

        unit.attackIntervalMultiplier *= Mathf.Max(0.01f, inst.attackIntervalMul);

        unit.RefreshStats();
        buffs.Add(inst);
    }

    private void RemoveBuffInternal(BuffInstance inst)
    {
        unit.bonusStrength -= inst.addStr;
        unit.bonusAgility -= inst.addAgi;
        unit.bonusIntelligence -= inst.addInt;

        unit.bonusStrengthRate -= inst.addStrRate;
        unit.bonusAgilityRate -= inst.addAgiRate;
        unit.bonusIntelligenceRate -= inst.addIntRate;

        // 역곱(0 방지)
        float mul = Mathf.Max(0.01f, inst.attackIntervalMul);
        unit.attackIntervalMultiplier /= mul;

        unit.RefreshStats();
    }

    public void SetTimedShield(double amount, float duration)
    {
        amount = System.Math.Max(0, amount);
        unit.maxShield = amount;
        unit.shield = amount;
        shieldEndTime = Time.time + Mathf.Max(0.01f, duration);
    }
}