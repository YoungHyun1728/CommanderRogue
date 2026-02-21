using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Adjacent Enemies DoT By MaxHP")]
public class SE_AdjacentEnemiesDotByMaxHp : SkillEffectDefinition
{
    [Range(0f, 1f)]
    public float dpsRatioOfMaxHp = 0.02f; // 예: 0.02 = maxHp의 2%를 초당 피해

    [Min(0.1f)]
    public float duration = 3f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.casterStatus == null) return;
        if (ctx.casterSkills == null) return;

        const string key = "AdjEnemiesDotByMaxHp";

        // 맞을 때마다 지속시간만 갱신(연장)
        ctx.casterStatus.RefreshDot(key, duration);

        // 코루틴은 1개만(중복 시작 금지)
        ctx.casterStatus.StartDotIfNotRunning(key, ctx.casterSkills, DotRoutine(ctx, key));
    }

    private IEnumerator DotRoutine(SkillContext ctx, string key)
    {
        float nextTickTime = Time.time + 1f;

        while (ctx.caster != null && ctx.caster.hp > 0 && Time.time < ctx.casterStatus.GetDotEndTime(key))
        {
            float wait = nextTickTime - Time.time;
            if (wait > 0f) yield return new WaitForSeconds(wait);

            if (ctx.caster == null || ctx.caster.hp <= 0) yield break;
            if (Time.time >= ctx.casterStatus.GetDotEndTime(key)) yield break;

            var enemies = ctx.GetAdjacentEnemies8();
            double dps = ctx.caster.maxHp * dpsRatioOfMaxHp;

            foreach (var e in enemies)
            {
                if (e == null) continue;
                if (e.hp <= 0) continue;
                e.ReceiveDamageWithResult(dps, ctx.caster);
            }

            nextTickTime += 1f;
            if (nextTickTime < Time.time) nextTickTime = Time.time + 1f;
        }
    }
}
