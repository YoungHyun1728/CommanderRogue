using UnityEngine;

public enum HealTarget
{
    Self,
    AllAllies,
    LowestAlly
}

// 체력을 치유하는 효과
[CreateAssetMenu(menuName = "Game/SkillEffects/Heal")]
public class SE_Heal : SkillEffectDefinition
{
    public HealTarget target = HealTarget.Self;
    public double flat = 0;
    public float casterMainStatMultiplier = 1.0f; // 주스탯 기반
    [Range(0f, 1f)] public float targetMaxHpPercent = 0f;

    public override void Execute(SkillContext ctx)
    {
        if (ctx.caster == null) return;

        // 대상 리스트 만들기
        var targets = new System.Collections.Generic.List<Unit>();

        if (target == HealTarget.Self)
        {
            targets.Add(ctx.caster);
        }
        else if (target == HealTarget.AllAllies)
        {
            var allies = ctx.GetAllies();
            foreach (var a in allies)
            {
                if (a == null) continue;
                var u = a.GetComponent<Unit>();
                if (u == null || u.hp <= 0) continue;
                targets.Add(u);
            }
        }
        else // LowestAlly
        {
            var allies = ctx.GetAllies();
            Unit best = null;
            double bestRatio = 999;
            foreach (var a in allies)
            {
                if (a == null) continue;
                var u = a.GetComponent<Unit>();
                if (u == null || u.hp <= 0) continue;
                double r = u.maxHp > 0 ? (u.hp / u.maxHp) : 1;
                if (r < bestRatio) { bestRatio = r; best = u; }
            }
            if (best != null) targets.Add(best);
        }

        if (targets.Count == 0) return;

        // 힐량 계산(대상별 maxHp%는 각자 기준)
        foreach (var u in targets)
        {
            if (u == null) continue;

            double heal = flat;
            heal += ctx.caster.GetMainStatTotal() * casterMainStatMultiplier;

            if (targetMaxHpPercent > 0f)
                heal = System.Math.Max(heal, u.maxHp * targetMaxHpPercent);

            u.Heal(heal);
        }
    }
}
