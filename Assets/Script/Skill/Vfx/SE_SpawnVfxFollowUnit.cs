using UnityEngine;

public enum FollowWho { Caster, Target }

// VFX를 유닛에 따라다니게 생성하는 스킬 효과 정의
[CreateAssetMenu(menuName="Game/SkillEffects/VFX Follow Unit")]
public class SE_SpawnVfxFollowUnit : SkillEffectDefinition
{
    public VfxType vfxType;
    public float duration = 1.0f;
    public Vector3 offset;
    public FollowWho follow = FollowWho.Caster;

    public override void Execute(SkillContext ctx)
    {
        if (VfxPoolManager.Instance == null) return;

        Transform t = null;
        if (follow == FollowWho.Caster && ctx.caster != null) t = ctx.caster.transform;
        if (follow == FollowWho.Target && ctx.targetUnit != null) t = ctx.targetUnit.transform;
        if (t == null) return;

        var vfx = VfxPoolManager.Instance.Get(vfxType, t.position + offset, Quaternion.identity);
        if (vfx == null) return;

        var f = vfx.GetComponent<FollowTarget>();
        if (f == null) f = vfx.gameObject.AddComponent<FollowTarget>();
        f.target = t;
        f.offset = offset;

        vfx.Play(duration);
    }
}
