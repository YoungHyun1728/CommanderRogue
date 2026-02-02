using UnityEngine;

public enum ShieldTarget
{
    Self,
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
        GameObject go = null;

        if (target == ShieldTarget.Self) go = ctx.caster.gameObject;
        else
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
            go = best != null ? best.gameObject : null;
        }

        if (go == null) return;

        double amount = flat + ctx.caster.GetMainStatTotal() * casterMainStatMultiplier;
        var st = go.GetComponent<UnitStatusEffectController>();
        st?.SetTimedShield(amount, duration);
    }
}
