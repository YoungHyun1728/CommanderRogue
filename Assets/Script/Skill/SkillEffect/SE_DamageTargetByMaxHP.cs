using UnityEngine;

// 최대체력에 비례한 피해를 입히는 효과
[CreateAssetMenu(menuName = "Game/SkillEffects/Damage Target (Maxhp)")]
public class SE_DamageTargetByMaxHP : SkillEffectDefinition
{
    public float percent = 0.02f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.targetUnit == null) return;
        double dmg = ctx.caster.maxHp * percent;
        ctx.targetUnit.ReceiveDamage(dmg, ctx.caster);
    }
}
