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
        public float attackSpeedMul;
    }

    private readonly List<BuffInstance> buffs = new();

    // "단일" 시간제 쉴드
    private float shieldEndTime = -1f;

    // ===== Damage Immunity (무적) =====
    private float immuneEndTime = -1f;
    public bool IsDamageImmune => Time.time < immuneEndTime;

    // ===== Revive (라운드/전투당 1회) =====
    private bool hasReviveOncePerBattle = false;
    private bool reviveUsedThisBattle = false;
    private float reviveHealPercent = 0f;
    public void SetReviveHealPercent(float percent)
    {
        reviveHealPercent = Mathf.Clamp01(percent);
    }
    public float ReviveHealPercent => reviveHealPercent;

    // ===== ReadyState Full Heal =====
    private bool healFullOnReady = false;

    // ===== Burn (받는 피해 증폭) =====
    private float burnEndTime = -1f;
    private float burnMult = 1f;

    // ===== Slow (이동속도 배율) =====
    private float slowEndTime = -1f;
    private float slowMult = 1f;

    // ===== Attack Slow (공격속도 배율) =====
    private float atkSlowEndTime = -1f;
    private float atkSlowSpeedMult = 1f;

    // ===== Poison (도트 데미지) =====
    private Coroutine poisonCo;

    // ===== Unique DoT routines (중복 금지) =====
    private readonly Dictionary<string, Coroutine> dotRoutines = new();
    private readonly Dictionary<string, float> dotEndTimes = new();

    void Awake()
    {
        unit = GetComponent<Unit>();
    }

    void Update()
    {
        if (unit == null) return;

        float now = Time.time;

        // Shield expire
        if (shieldEndTime > 0f && now >= shieldEndTime)
        {
            shieldEndTime = -1f;
            unit.maxShield = 0;
            unit.shield = 0;
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

        // AttackSlow expire
        if (atkSlowEndTime > 0f && now >= atkSlowEndTime)
        {
            atkSlowEndTime = -1f;

            // 다른 배율(버프 등)을 보존하기 위해, 내 배율만 제거
            unit.attackSpeedMultiplier /= Mathf.Max(0.01f, atkSlowSpeedMult);
            atkSlowSpeedMult = 1f;
        }

        // Buff expire
        if (buffs.Count > 0)
        {
            for (int i = buffs.Count - 1; i >= 0; --i)
            {
                if (now >= buffs[i].endTime)
                {
                    RemoveBuffInternal(buffs[i]);
                    buffs.RemoveAt(i);
                }
            }
        }

        // Unique dots expire
        if (dotEndTimes.Count > 0)
        {
            // 안전하게 키 목록 복사
            var keys = new List<string>(dotEndTimes.Keys);
            foreach (var k in keys)
            {
                if (now >= dotEndTimes[k])
                {
                    StopDot(k);
                }
            }
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
            attackSpeedMul = def.attackSpeedMultiplier
        };

        // 적용
        unit.bonusStrength += inst.addStr;
        unit.bonusAgility += inst.addAgi;
        unit.bonusIntelligence += inst.addInt;

        unit.bonusStrengthRate += inst.addStrRate;
        unit.bonusAgilityRate += inst.addAgiRate;
        unit.bonusIntelligenceRate += inst.addIntRate;

        unit.attackSpeedMultiplier *= Mathf.Max(0.01f, inst.attackSpeedMul);

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
        float mul = Mathf.Max(0.01f, inst.attackSpeedMul);
        unit.attackSpeedMultiplier /= mul;

        unit.RefreshStats();
    }


    // ====== Skill helper APIs ======

    // 8) 피해 면역(무적)
    public void SetDamageImmunity(float duration)
    {
        immuneEndTime = Mathf.Max(immuneEndTime, Time.time + Mathf.Max(0.01f, duration));
    }

    // 6) 죽음 극복(전투/라운드당 1회)
    // 스킬/패시브에서 1회 부활 능력을 켜둘 때 호출
    public void EnableReviveOncePerBattle(bool enabled)
    {
        hasReviveOncePerBattle = enabled;
        if (!enabled)
        {
            reviveUsedThisBattle = false;
        }
    }

    // HP가 0이 되려는 순간 호출해서 부활 처리 여부를 리턴
    public bool TryConsumeRevive()
    {
        if (!hasReviveOncePerBattle) return false;
        if (reviveUsedThisBattle) return false;

        reviveUsedThisBattle = true;
        return true;
    }

    // 전투(라운드) 시작 시 호출해서 1회 부활 사용여부 초기화
    public void ResetBattleFlags()
    {
        reviveUsedThisBattle = false;
    }

    // ReadyState에서 풀힐
    public void EnableHealFullOnReady(bool enabled)
    {
        healFullOnReady = enabled;
    }

    // RunManager.EnterReady() 같은 곳에서 호출
    public void OnEnterReadyState()
    {
        if (!healFullOnReady || unit == null) return;
        unit.Heal(unit.maxHp); // Heal()이 maxHp clamp 하므로 full heal
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

    public void ApplyAttackSlow(float speedMult, float duration) // 공격속도 감소 (예: 0.7 = 30% 느림)
    {
        if (unit == null) return;

        speedMult = Mathf.Clamp(speedMult, 0.01f, 100f);

        // 더 느린 값(더 작은 배율)을 우선 예: 0.6이 0.8보다 강함
        float newMult = Mathf.Min(atkSlowSpeedMult, speedMult);

        // 기존 atkSlowSpeedMult를 제거하고 새 값을 곱해준다(다른 버프/디버프 배율 보존)
        if (!Mathf.Approximately(newMult, atkSlowSpeedMult))
        {
            unit.attackSpeedMultiplier /= Mathf.Max(0.01f, atkSlowSpeedMult);
            unit.attackSpeedMultiplier *= newMult;
            atkSlowSpeedMult = newMult;
        }

        atkSlowEndTime = Mathf.Max(atkSlowEndTime, Time.time + Mathf.Max(0.01f, duration));

        FloatingTextPoolManager.Instance.ShowStatus(
            transform, "공속감소", new Vector3(0, 1.1f, 0)
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
        float end = Time.time + Mathf.Max(0.01f, duration);

        while (Time.time < end)
        {
            if (unit == null) yield break;

            // 1초당 dps
            unit.TakeDamage(dps);
            yield return new WaitForSeconds(1f);
        }
    }

    public void RefreshDot(string key, float duration)
    {
        if (string.IsNullOrEmpty(key)) return;

        float newEnd = Time.time + Mathf.Max(0.01f, duration);

        if (dotEndTimes.TryGetValue(key, out float oldEnd))
            dotEndTimes[key] = Mathf.Max(oldEnd, newEnd);  // 연장 / 갱신
        else
            dotEndTimes[key] = newEnd;
    }

    public float GetDotEndTime(string key)
    {
        if (string.IsNullOrEmpty(key)) return -1f;
        if (dotEndTimes.TryGetValue(key, out float t)) return t;
        return -1f;
    }

    public void StartDotIfNotRunning(string key, MonoBehaviour host, IEnumerator routine)
    {
        if (string.IsNullOrEmpty(key) || host == null || routine == null) return;

        if (dotRoutines.ContainsKey(key)) return;
        dotRoutines[key] = host.StartCoroutine(routine);
    }

    public void StopDot(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (dotRoutines.TryGetValue(key, out Coroutine co))
        {
            if (co != null) StopCoroutine(co);
            dotRoutines.Remove(key);
        }

        if (dotEndTimes.ContainsKey(key))
            dotEndTimes.Remove(key);
    }
}