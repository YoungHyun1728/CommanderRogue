using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 타겟이 있든 없든, 캐릭터가 바라보는 방향으로 투사체를 발사하는 스킬 효과

[CreateAssetMenu(menuName="Game/SkillEffects/Spawn Projectile Forward")]
public class SE_SpawnProjectileForward : SkillEffectDefinition
{
    public ProjectileType projectileType;
    public float speed = 12f;
    public float lifeTime = 2.5f;

    [Header("OnHit Effects")]
    public List<SkillEffectDefinition> onHitEffects = new();

    [Header("Hit VFX (선택)")]
    public VfxType hitVfx = VfxType.None;
    public float hitVfxDuration = 0.4f;

    [Header("Pierce")]
    public bool piercing = true;
    public int maxHits = 999;

    public Vector3 spawnOffset;

    public override void Execute(SkillContext ctx)
    {
        if (ProjectilePoolManager.Instance == null) return;
        if (ctx.caster == null) return;

        Vector3 spawnPos = ctx.casterFsm.GetProjectileSpawnWorldPos() + spawnOffset;

        var proj = ProjectilePoolManager.Instance.Get(projectileType, spawnPos, Quaternion.identity);
        if (proj == null) return;

        // 방향 결정: 타겟이 있으면 그 방향, 없으면 "캐릭터가 바라보는 방향"
        Vector3 dir;
        if(ctx.targetGO != null)
        {
            // 타겟 에임포인트(없으면 collider center)
            Vector3 targetPos = ctx.targetGO.transform.position;

            var tfsm = ctx.targetGO.GetComponent<UnitFSM>();
            if (tfsm != null && tfsm.AimPoint != null)
                targetPos = tfsm.AimPoint.position;
            else
            {
                var col = ctx.targetGO.GetComponent<Collider2D>();
                if (col != null) targetPos = col.bounds.center;
            }

            dir = (targetPos - spawnPos).normalized;
        }
        else
        {
            // 타겟 없으면 바라보는 방향
            // (rect.right가 플립 반영이면 이게 편함)
            dir = ctx.casterFsm.transform.right;
        }

        proj.InitSkillForward(
            casterUnit: ctx.caster,
            dir: dir,
            speedOverride: speed,
            lifeTimeOverride: lifeTime,
            onHitEffects: onHitEffects,
            hitVfxType: hitVfx,
            hitVfxDuration: hitVfxDuration,
            piercing: piercing,
            maxHits: maxHits
        );
    }
}