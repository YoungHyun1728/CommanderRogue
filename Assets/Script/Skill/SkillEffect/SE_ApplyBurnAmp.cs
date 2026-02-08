using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Apply Burn Amp")]
public class SE_ApplyBurnAmp : SkillEffectDefinition
{
    public float mult = 1.2f;
    public float duration = 5f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.targetUnit == null) return;
        ctx.targetUnit.GetComponent<UnitStatusEffectController>()?.ApplyBurnAmp(mult, duration);
    }
}
