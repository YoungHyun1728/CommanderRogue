using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Apply Stun")]
public class SE_ApplyStun : SkillEffectDefinition
{
    public float duration = 1f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.targetFsm == null) return;
        ctx.targetFsm.ApplyStun(duration);
    }
}
