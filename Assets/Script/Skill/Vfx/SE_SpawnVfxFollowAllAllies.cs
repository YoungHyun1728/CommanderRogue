using UnityEngine;

[CreateAssetMenu(menuName="Game/VfxSkillEffects/VFX Follow All Allies")]
public class SE_SpawnVfxFollowAllAllies : SkillEffectDefinition
{
    public VfxType vfxType;
    public float duration = 1.0f;
    public Vector3 offset;
    public bool includeCaster = true;

    public override void Execute(SkillContext ctx)
    {
        if (VfxPoolManager.Instance == null) return;
        if (ctx.caster == null) return;

        // SkillContext에 이미 "모든 아군 Unit" 가져오는 헬퍼가 있음
        var allies = ctx.GetAlliedUnits(includeCaster);

        foreach (var ally in allies)
        {
            if (ally == null) continue;

            Transform t = ally.transform;
            var vfx = VfxPoolManager.Instance.Get(vfxType, t.position + offset, Quaternion.identity);
            if (vfx == null) continue;

            var f = vfx.GetComponent<FollowTarget>();
            if (f == null) f = vfx.gameObject.AddComponent<FollowTarget>();

            f.target = t;
            f.offset = offset;

            vfx.Play(duration);
        }
    }
}