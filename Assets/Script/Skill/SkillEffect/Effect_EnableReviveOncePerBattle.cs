using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Enable Revive Once Per Battle")]
public class SE_EnableReviveOncePerBattle : SkillEffectDefinition
{
    [Range(0f, 1f)]
    public float healPercent = 0.3f; // 예: 0.3 = 최대체력 30%로 즉시 회복

    public override void Execute(SkillContext ctx)
    {
        if (ctx.casterStatus == null) return;

        ctx.casterStatus.EnableReviveOncePerBattle(true);
        ctx.casterStatus.SetReviveHealPercent(healPercent);
    }
}