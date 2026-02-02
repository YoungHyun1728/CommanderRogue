using System.Collections.Generic;
using UnityEngine;

public enum SkillSlot
{
    FullManaActive,   // 풀마나 스킬 (유닛당 1개만)
    Passive           // 패시브 (여러 개)
}

public enum PassiveTrigger
{
    None,
    OnBasicAttackHit,
    OnTakeDamage
}

[CreateAssetMenu(menuName = "Game/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    public string skillId;
    public string displayName;

    public SkillSlot slot = SkillSlot.Passive;

    [Header("Passive Only")]
    public PassiveTrigger trigger = PassiveTrigger.None;
    [Range(0f, 1f)] public float triggerChance = 1f;

    [Header("Common")]
    public float cooldown = 0f;
    public List<SkillEffectDefinition> effects = new();

    public void Execute(SkillContext ctx)
    {
        foreach (var e in effects)
        {
            if (e == null) continue;
            e.Execute(ctx);
        }
    }
}