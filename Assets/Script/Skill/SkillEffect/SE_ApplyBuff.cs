using UnityEngine;

public enum BuffTarget
{
    Self,
    TargetEnemy,
    LowestAlly
}

[CreateAssetMenu(menuName = "Game/SkillEffects/Apply Buff")]
public class SE_ApplyBuff : SkillEffectDefinition
{
    public BuffDefinition buff;
    public BuffTarget target = BuffTarget.Self;

    public override void Execute(SkillContext ctx)
    {
        GameObject go = null;

        if (target == BuffTarget.Self) go = ctx.caster != null ? ctx.caster.gameObject : null;
        else if (target == BuffTarget.TargetEnemy) go = ctx.targetGO;
        else
        {
            // lowest ally
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
            go = best != null ? best.gameObject : null;
        }

        if (go == null) return;

        var st = go.GetComponent<UnitStatusEffectController>();
        st?.ApplyBuff(buff);
    }
}
