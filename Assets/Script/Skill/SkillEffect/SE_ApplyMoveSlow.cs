using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Apply Move Slow")]
public class SE_ApplyMoveSlow : SkillEffectDefinition
{
    public float mult = 0.7f;
    public float duration = 3f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.targetUnit == null) return;
        ctx.targetUnit.GetComponent<UnitStatusEffectController>()?.ApplyMoveSlow(mult, duration);
    }
}
