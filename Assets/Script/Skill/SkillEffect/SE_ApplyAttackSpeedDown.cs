using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/ApplyAttackSlow")]
public class SE_ApplyAttackSpeedDown : SkillEffectDefinition
{
    public float mult = 0.7f;
    public float duration = 3f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.targetUnit == null) return;
        ctx.targetUnit.GetComponent<UnitStatusEffectController>()?.ApplyAttackSlow(mult, duration);
    }
}
