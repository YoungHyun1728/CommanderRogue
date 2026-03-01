using System.Collections.Generic;
using UnityEngine;

public enum SkillSlot
{
    FullManaActive,   // 풀마나 스킬 (유닛당 1개만)
    Passive           // 패시브 (여러 개)
}

public enum PassiveTrigger
{
    None,
    OnBasicAttackHit,
    OnTakeDamage
}

/// <summary>
/// 스킬 1개가 가진 "효과 목록"을 실행합니다.
/// 타겟이 여러 명인 스킬은 Execute 직전에 SkillContext.targetGOs 를 채워서
/// 각 SkillEffectDefinition들이 ctx.EnumerateTargets()로 소비할 수 있게 합니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    public string skillId;
    public string displayName;
    public Sprite icon;
    [TextArea] public string description;

    public SkillSlot slot = SkillSlot.Passive;

    [Header("Passive Only")]
    public PassiveTrigger trigger = PassiveTrigger.None;
    [Range(0f, 1f)] public float triggerChance = 1f;

    [Header("Common")]
    public float cooldown = 0f;
    public List<SkillEffectDefinition> effects = new();

    // =============================
    // Targeting (멀티 타겟 채우기)
    // =============================
    public enum TargetExpandMode
    {
        /// <summary>
        /// (기본) "현재 타겟 1명 + 가까운 적 n명" (총 targetCount명)
        /// 예: targetCount=2 => 현재 타겟 + 그 다음 가까운 적 1명
        /// </summary>
        CurrentPlusNearestEnemies,
    }

    [Header("Targeting (Optional)")]
    [Tooltip("체크하면 Execute 직전에 targetGOs를 자동으로 확장합니다. (투사체/단일타겟 스킬을 멀티타겟으로 쓰기 용)")]
    public bool autoExpandTargets = false;

    [Min(0)]
    [Tooltip("autoExpandTargets가 켜져 있을 때, 최종 타겟 수(0이면 타겟 없음).")]
    public int targetCount = 1;

    [Tooltip("타겟 확장 방식")]
    public TargetExpandMode expandMode = TargetExpandMode.CurrentPlusNearestEnemies;

    public void Execute(SkillContext ctx)
    {
        SkillContext execCtx = ctx;

        // 타겟 자동 확장: 컨텍스트에 targetGO만 들어왔더라도, 스킬 설정대로 targetGOs를 만들어줌
        if (autoExpandTargets && targetCount > 1)
        {
            var targets = BuildExpandedTargets(ctx);

            // targetCount 이상 뽑혔을 때만 새 컨텍스트로 실행 (없으면 원래 ctx로 실행)
            if (targets != null && targets.Count > 0)
            {
                // targetCount로 컷
                if (targets.Count > targetCount)
                    targets.RemoveRange(targetCount, targets.Count - targetCount);

                execCtx = new SkillContext(ctx.caster, ctx.casterFsm, ctx.casterSkills, ctx.casterStatus, targets);
                execCtx.param = ctx.param; // 생성자에서 0으로 초기화되므로 전달값 복원
            }
        }

        foreach (var e in effects)
        {
            if (e == null) continue;
            e.Execute(execCtx);
        }
    }

    private List<GameObject> BuildExpandedTargets(SkillContext ctx)
    {
        var result = new List<GameObject>();

        // 1) 기존 타겟 먼저 담기 (중복 제거)
        if (ctx.targetGOs != null && ctx.targetGOs.Count > 0)
        {
            for (int i = 0; i < ctx.targetGOs.Count; i++)
            {
                var go = ctx.targetGOs[i];
                if (go == null) continue;
                if (result.Contains(go)) continue;
                result.Add(go);
                if (result.Count >= targetCount) return result;
            }
        }
        else if (ctx.targetGO != null)
        {
            result.Add(ctx.targetGO);
        }

        // 2) 추가 타겟 채우기
        int need = targetCount - result.Count;
        if (need <= 0) return result;

        switch (expandMode)
        {
            case TargetExpandMode.CurrentPlusNearestEnemies:
            default:
            {
                // SkillContext에 구현해둔 헬퍼 사용:
                // "현재 타겟 + 가까운 적 n명" 을 Unit 리스트로 받음
                var units = ctx.GetCurrentPlusNEnemies(extraCount: targetCount - 1);
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == null) continue;
                    var go = u.gameObject;
                    if (go == null) continue;
                    if (result.Contains(go)) continue;
                    result.Add(go);
                    if (result.Count >= targetCount) break;
                }
                break;
            }
        }

        return result;
    }

    public string BuildRuntimeDescription(Unit unit)
    {
        return SkillTooltipTemplate.Render(description, unit);
    }
}
