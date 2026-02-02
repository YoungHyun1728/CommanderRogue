using System.Collections.Generic;
using UnityEngine;

public class UnitSkillSystem : MonoBehaviour
{
    [Header("Skills")]
    [SerializeField] private SkillDefinition fullManaActive;  // 유닛당 1개
    [SerializeField] private List<SkillDefinition> passives = new();

    private Unit unit;
    private UnitFSM fsm;
    private UnitStatusEffectController status;

    private readonly Dictionary<SkillDefinition, float> nextReadyTime = new();

    // 강화공격(다음 N회) 상태
    private int enhancedHitsLeft = 0;
    private float enhancedMainStatMultiplier = 0f;

    void Awake()
    {
        unit = GetComponent<Unit>();
        fsm = GetComponent<UnitFSM>();
        status = GetComponent<UnitStatusEffectController>();
    }

    public void ReplaceFullManaSkill(SkillDefinition newSkill)
    {
        fullManaActive = newSkill; // 기존 풀마나 스킬 제거 후 교체
    }

    public void AddPassive(SkillDefinition passive)
    {
        if (passive == null) return;
        if (!passives.Contains(passive)) passives.Add(passive);
    }

    // 강화공격: 다음 N회 공격에 "주스탯*배수" 추가
    public void SetEnhancedNextAttacks(int hits, float mainStatMultiplier)
    {
        enhancedHitsLeft = Mathf.Max(0, hits);
        enhancedMainStatMultiplier = mainStatMultiplier;
    }

    // Unit에서 기본공격 데미지 계산할 때 호출됨
    public double ConsumeEnhancedBonusDamage(Unit target)
    {
        if (enhancedHitsLeft <= 0) return 0;

        enhancedHitsLeft--;
        double bonus = unit.GetMainStatTotal() * enhancedMainStatMultiplier;
        if (enhancedHitsLeft <= 0)
            enhancedMainStatMultiplier = 0f;

        return bonus;
    }

    public bool TryCastFullManaSkill(GameObject targetGO)
    {
        if (fullManaActive == null) return false;
        if (unit.mp < unit.maxMp) return false;

        if (!IsReady(fullManaActive)) return false;

        // mp 소모: "풀마나 스킬은 하나" 규칙
        unit.mp = 0;

        var ctx = new SkillContext(unit, fsm, this, status, targetGO);
        fullManaActive.Execute(ctx);

        SetCooldown(fullManaActive);
        return true;
    }
    

    public void NotifyBasicAttackHit(GameObject targetGO)
    {
        // 패시브 트리거
        foreach (var p in passives)
        {
            if (p == null) continue;
            if (p.trigger != PassiveTrigger.OnBasicAttackHit) continue; 
            if (!IsReady(p)) continue;

            if (Random.value <= p.triggerChance)
            {
                var ctx = new SkillContext(unit, fsm, this, status, targetGO);
                p.Execute(ctx);
                SetCooldown(p);
            }
        }
    }
    
    public void NotifyTakeDamage(GameObject attackerGO)
    {
        foreach (var p in passives)
        {
            if (p == null) continue;
            if (p.trigger != PassiveTrigger.OnTakeDamage) continue;
            if (!IsReady(p)) continue;

            if (Random.value <= p.triggerChance)
            {
                var ctx = new SkillContext(unit, fsm, this, status, attackerGO);
                p.Execute(ctx);
                SetCooldown(p);
            }
        }
    }

    private bool IsReady(SkillDefinition s)
    {
        if (s.cooldown <= 0f) return true;
        if (!nextReadyTime.TryGetValue(s, out var t)) return true;
        return Time.time >= t;
    }

    private void SetCooldown(SkillDefinition s)
    {
        if (s.cooldown <= 0f) return;
        nextReadyTime[s] = Time.time + s.cooldown;
    }
}
