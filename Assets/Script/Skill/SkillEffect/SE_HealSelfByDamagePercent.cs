using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Heal Self By Damage Percent")]
public class SE_HealSelfByDamagePercent : SkillEffectDefinition
{
    [Range(0f, 5f)]
    public float healPercentOfDamage = 0.1f; // 예: 0.1 = 피해량의 10%

    public override void Execute(SkillContext ctx)
    {
        if (ctx.caster == null) return;

        double dealt = ctx.param; // UnitSkillSystem.NotifyBasicAttackHit에서 넣어준 실제 피해량
        double healAmount = dealt * healPercentOfDamage;
        if (healAmount <= 0d) return;

        ctx.caster.Heal(healAmount);
    }
}
