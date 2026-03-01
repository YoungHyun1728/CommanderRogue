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

        // 멀티 타겟 지원:
        // - 타겟이 여러 명이면 각 타겟 방향으로 한 발씩
        // - 타겟이 없으면 바라보는 방향으로 한 발
        bool hasAnyTarget = false;
        foreach (var t in ctx.EnumerateTargets())
        {
            if (t == null) continue;
            hasAnyTarget = true;

            var proj = ProjectilePoolManager.Instance.Get(projectileType, spawnPos, Quaternion.identity);
            if (proj == null) continue;

            Vector3 targetPos = t.transform.position;

            var tfsm = t.GetComponent<UnitFSM>();
            if (tfsm != null && tfsm.AimPoint != null)
                targetPos = tfsm.AimPoint.position;
            else
            {
                var col = t.GetComponent<Collider2D>();
                if (col != null) targetPos = col.bounds.center;
            }

            Vector3 dir = (targetPos - spawnPos).normalized;

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

        if (hasAnyTarget) return;

        // 타겟이 없으면 바라보는 방향으로 한 발
        {
            var proj = ProjectilePoolManager.Instance.Get(projectileType, spawnPos, Quaternion.identity);
            if (proj == null) return;

            Vector3 dir = ctx.casterFsm.transform.right;

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
}