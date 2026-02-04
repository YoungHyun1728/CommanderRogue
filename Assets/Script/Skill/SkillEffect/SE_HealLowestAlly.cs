using UnityEngine;

// 체력 비율이 가장 낮은 아군을 치유하는 효과
[CreateAssetMenu(menuName = "Game/SkillEffects/Heal Lowest Ally (HP Ratio)")]
public class SE_HealLowestAlly : SkillEffectDefinition
{
    public double flat = 0;
    public float casterMainStatMultiplier = 1.0f; // 주스탯 기반
    [Range(0f, 1f)] public float targetMaxHpPercent = 0f;

    public override void Execute(SkillContext ctx)
    {
        var allies = ctx.GetAllies();
        Unit best = null;
        double bestRatio = 999;

        foreach (var go in allies)
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            if (u.hp <= 0) continue;

            double ratio = u.maxHp > 0 ? (u.hp / u.maxHp) : 1;
            if (ratio < bestRatio)
            {
                bestRatio = ratio;
                best = u;
            }
        }

        if (best == null) return;

        double heal = flat;
        heal += ctx.caster.GetMainStatTotal() * casterMainStatMultiplier;
        if (targetMaxHpPercent > 0f)
            heal = System.Math.Max(heal, best.maxHp * targetMaxHpPercent);

        best.Heal(heal);
    }
}
