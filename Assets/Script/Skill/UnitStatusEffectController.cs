using System.Collections;
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

    // ===== Burn (받는 피해 증폭) =====
    private float burnEndTime = -1f;
    private float burnMult = 1f;

    // ===== Slow (이동속도 배율) =====
    private float slowEndTime = -1f;
    private float slowMult = 1f;

    // ===== Poison (도트 데미지) =====
    private Coroutine poisonCo;

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

        // Burn expire
        if (burnEndTime > 0f && now >= burnEndTime)
        {
            burnEndTime = -1f;
            burnMult = 1f;
            unit.incomingDamageMultiplier = 1f;
        }

        // Slow expire
        if (slowEndTime > 0f && now >= slowEndTime)
        {
            slowEndTime = -1f;
            slowMult = 1f;
            unit.moveSpeedMultiplier = 1f;    
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

    public void SetTimedShield(double amount, float duration) // 
    {
        amount = System.Math.Max(0, amount);
        unit.maxShield = amount;
        unit.shield = amount;
        shieldEndTime = Time.time + Mathf.Max(0.01f, duration);
    }

    // 디버프 상태이상 적용 메서드들
    public void ApplyBurnAmp(float mult, float duration) // 받는 피해량 증폭
    {
        if (unit == null) return;

        // 더 강한 증폭을 우선
        burnMult = Mathf.Max(burnMult, mult);
        burnEndTime = Mathf.Max(burnEndTime, Time.time + Mathf.Max(0.01f, duration));

        unit.incomingDamageMultiplier = burnMult;
        FloatingTextPoolManager.Instance.ShowStatus(
            transform, "착화", new Vector3(0, 1.1f, 0)
        );
    }

    public void ApplyMoveSlow(float mult, float duration) // 이동속도 감소
    {
        if (unit == null) return;

        // 더 느린 값(더 작은 배율)을 우선(정책) 예: 0.6이 0.8보다 강함
        slowMult = Mathf.Min(slowMult, mult);
        slowEndTime = Mathf.Max(slowEndTime, Time.time + Mathf.Max(0.01f, duration));

        unit.moveSpeedMultiplier = slowMult;
        FloatingTextPoolManager.Instance.ShowStatus(
            transform, "느려짐", new Vector3(0, 1.1f, 0)
        );
    }

    public void ApplyPoison(double dps, float duration) // 도트 데미지
    {
        if (unit == null) return;

        if (poisonCo != null) StopCoroutine(poisonCo);
        poisonCo = StartCoroutine(PoisonRoutine(dps, duration));
        FloatingTextPoolManager.Instance.ShowStatus(
            transform, "중독", new Vector3(0, 1.1f, 0)
        );
    }

    private IEnumerator PoisonRoutine(double dps, float duration)
    {
        float t = 0f;

        while (t < duration && unit.hp > 0)
        {
            // attacker는 null로 둬서 피격트리거 방지
            unit.ReceiveDamage(dps, null);

            yield return new WaitForSeconds(1f);
            t += 1f;
        }

        poisonCo = null;
    }

}