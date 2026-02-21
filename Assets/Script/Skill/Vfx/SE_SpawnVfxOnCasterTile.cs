using UnityEngine;

// 캐스터 유닛이 위치한 타일에 VFX를 생성하는 스킬 이펙트
[CreateAssetMenu(menuName="Game/VfxSkillEffects/VFX On Caster Tile")]
public class SE_SpawnVfxOnCasterTile : SkillEffectDefinition
{
    public VfxType vfxType;
    public float duration = 0.5f;
    public Vector3 offset;

    public override void Execute(SkillContext ctx)
    {
        if (VfxPoolManager.Instance == null) return;
        if (ctx.tileMap == null || ctx.casterFsm == null) return;

        Vector2Int tile = ctx.casterFsm.currentTilePosition;
        Vector3 pos = ctx.tileMap.GetTileCenterWorld(tile) + offset;

        var vfx = VfxPoolManager.Instance.Get(vfxType, pos, Quaternion.identity);
        if (vfx != null) vfx.Play(duration);
    }
    
}
