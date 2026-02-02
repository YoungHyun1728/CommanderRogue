using UnityEngine;

// 대상의 최대 체력 비율에 따른 피해를 입히는 효과
[CreateAssetMenu(menuName = "Game/SkillEffects/Damage Target (Target MaxHp %)")]
public class SE_DamageTargetByTargetMaxHp : SkillEffectDefinition
{
    [Range(0f, 1f)] public float percent = 0.1f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.targetUnit == null) return;
        double dmg = ctx.targetUnit.maxHp * percent;
        ctx.targetUnit.ReceiveDamage(dmg, ctx.caster);
    }
}
