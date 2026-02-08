using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Apply Poison")]
public class SE_ApplyPoison : SkillEffectDefinition
{
    public float duration = 5f;
    public double flatDps = 5;
    public float casterMainStatMult = 0.3f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.targetUnit == null || ctx.caster == null) return;
        double dps = flatDps + ctx.caster.GetMainStatTotal() * casterMainStatMult;
        ctx.targetUnit.GetComponent<UnitStatusEffectController>()?.ApplyPoison(dps, duration);
    }
}

