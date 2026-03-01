using System.Collections.Generic;
using UnityEngine;

// 타겟있는 스킬이 발동할 때, 타겟 위치로 투사체를 발사하는 스킬 효과
[CreateAssetMenu(menuName="Game/SkillEffects/Spawn Projectile To Target")]
public class SE_SpawnProjectileToTarget :  SkillEffectDefinition
{
    public ProjectileType projectileType;
    public float speed = 10f;
    public float lifeTime = 3f;

    [Header("OnHit Effects (여기에 데미지/디버프 넣기)")]
    public List<SkillEffectDefinition> onHitEffects = new();

    [Header("Hit VFX (선택)")]
    public VfxType hitVfx = VfxType.None;
    public float hitVfxDuration = 0.4f;

    [Header("Pierce")]
    public bool piercing = false;
    public int maxHits = 1;

    public Vector3 spawnOffset;

    public override void Execute(SkillContext ctx)
    {
        if (ProjectilePoolManager.Instance == null) return;
        if (ctx.caster == null) return;

        // 멀티 타겟 지원: 타겟이 여러 명이면 각각에게 투사체를 발사
        bool spawnedAny = false;
        foreach (var t in ctx.EnumerateTargets())
        {
            if (t == null) continue;

            Vector3 spawnPos = ctx.caster.transform.position + spawnOffset;

            var proj = ProjectilePoolManager.Instance.Get(projectileType, spawnPos, Quaternion.identity);
            if (proj == null) continue;

            proj.InitSkillToTarget(
                casterUnit: ctx.caster,
                target: t,
                speedOverride: speed,
                lifeTimeOverride: lifeTime,
                onHitEffects: onHitEffects,
                hitVfxType: hitVfx,
                hitVfxDuration: hitVfxDuration,
                piercing: piercing,
                maxHits: maxHits
            );

            spawnedAny = true;
        }

        // 타겟이 없으면 아무것도 하지 않음(기존 동작 유지)
        if (!spawnedAny) return;
    }

}
