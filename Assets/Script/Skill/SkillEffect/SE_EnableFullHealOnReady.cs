using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Enable Full Heal On Ready")]
public class SE_EnableFullHealOnReady : SkillEffectDefinition
{
    public override void Execute(SkillContext ctx)
    {
        if (ctx.casterStatus == null) return;
        ctx.casterStatus.EnableHealFullOnReady(true);
    }
}