using UnityEngine;
using System.Collections.Generic;

public enum ShieldTarget
{
    Self,
    AllAllies,
    LowestAlly,
}

[CreateAssetMenu(menuName = "Game/SkillEffects/Add Shield")]
public class SE_AddShield : SkillEffectDefinition
{
    public ShieldTarget target = ShieldTarget.Self;

    public double flat = 200;
    public float casterMainStatMultiplier = 0f;
    public float duration = 4f;

    public override void Execute(SkillContext ctx)
    {
        var targets = new List<GameObject>();

        if (target == ShieldTarget.Self)
        {
            if (ctx.caster != null) targets.Add(ctx.caster.gameObject);
        }
        else if (target == ShieldTarget.AllAllies)
        {
            var allies = ctx.GetAllies();
            foreach (var a in allies)
            {
                if (a == null) continue;
                var u = a.GetComponent<Unit>();
                if (u == null || u.hp <= 0) continue;
                targets.Add(a);
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
            if (best != null) targets.Add(best.gameObject);
        }

        if (targets.Count == 0) return;

        double amount = flat + ctx.caster.GetMainStatTotal() * casterMainStatMultiplier;
        foreach (var go in targets)
        {
            if (go == null) continue;
            var st = go.GetComponent<UnitStatusEffectController>();
            st?.SetTimedShield(amount, duration);
        }
    }
}
