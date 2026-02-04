using UnityEngine;

// 주 능력치 기반으로 대상에게 피해를 입히는 효과
[CreateAssetMenu(menuName = "Game/SkillEffects/Damage Target (MainStat)")]
public class SE_DamageTargetByMainStat : SkillEffectDefinition
{
    public float mainStatMultiplier = 2.0f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.targetUnit == null) return;
        double dmg = ctx.caster.GetMainStatTotal() * mainStatMultiplier;
        ctx.targetUnit.ReceiveDamage(dmg, ctx.caster);
    }
}
