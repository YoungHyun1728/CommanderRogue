using UnityEngine;
using System.Collections.Generic;

public enum BuffTarget
{
    Self,
    AllAllies,
    LowestAlly
}

[CreateAssetMenu(menuName = "Game/SkillEffects/Apply Buff")]
public class SE_ApplyBuff : SkillEffectDefinition
{
    public BuffDefinition buff;
    public BuffTarget target = BuffTarget.Self;

    public override void Execute(SkillContext ctx)
    {
        if (buff == null) return;

        var targets = new List<GameObject>();

        if (target == BuffTarget.Self)
        {
            if (ctx.caster != null) targets.Add(ctx.caster.gameObject);
        }
        else if (target == BuffTarget.AllAllies)
        {
            // 전체 아군
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

        foreach (var go in targets)
        {
            if (go == null) continue;
            var st = go.GetComponent<UnitStatusEffectController>();
            st?.ApplyBuff(buff);
        }
    }
}