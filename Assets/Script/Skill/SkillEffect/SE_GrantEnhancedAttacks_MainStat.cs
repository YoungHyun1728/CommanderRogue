using UnityEngine;

// 주어진 횟수만큼 강화된 공격을 부여하는 효과 (주 능력치 기반)
[CreateAssetMenu(menuName = "Game/SkillEffects/Grant Enhanced Attacks (MainStat)")]
public class SE_GrantEnhancedAttacks_MainStat : SkillEffectDefinition
{
    public int hitCount = 3;
    public float mainStatMultiplier = 1.0f;

    public override void Execute(SkillContext ctx)
    {
        ctx.casterSkills?.SetEnhancedNextAttacks(hitCount, mainStatMultiplier);
        FloatingTextPoolManager.Instance.ShowStatus(
            ctx.casterFsm.transform, "공격 강화", new Vector3(0, 1.1f, 0)
        );  
    }
}
