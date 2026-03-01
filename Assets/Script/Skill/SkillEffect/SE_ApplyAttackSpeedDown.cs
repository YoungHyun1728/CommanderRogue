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
        FloatingTextPoolManager.Instance?.ShowStatus(
            ctx.targetFsm.transform,
            "공격속도 감소",
            new Vector3(0f, 1.2f, 0f)
        );
    }
}
