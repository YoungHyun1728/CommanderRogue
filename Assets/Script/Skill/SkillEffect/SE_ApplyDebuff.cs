using UnityEngine;
using System.Collections.Generic;

public enum DebuffTarget
{
    Self,
    TargetEnemy,
    AllEnemies
}

[CreateAssetMenu(menuName = "Game/SkillEffects/Apply Debuff")]
public class SE_ApplyDebuff : SkillEffectDefinition
{
    // 현재는 BuffDefinition을 그대로 사용(버프/디버프 공용)하고,
    // 에디터에서 구분해서 쓰기 위한 전용 이펙트입니다.
    public BuffDefinition debuff;
    public DebuffTarget target = DebuffTarget.TargetEnemy;

    public override void Execute(SkillContext ctx)
    {
        if (debuff == null) return;

        var targets = new List<GameObject>();

        if (target == DebuffTarget.Self)
        {
            if (ctx.caster != null) targets.Add(ctx.caster.gameObject);
        }
        else if (target == DebuffTarget.TargetEnemy)
        {
            // 컨텍스트에 들어온 타겟들(멀티 포함)
            foreach (var t in ctx.EnumerateTargets())
                if (t != null) targets.Add(t);
        }
        else if (target == DebuffTarget.AllEnemies)
        {
            // 전체 적군
            var enemies = ctx.GetEnemies();
            foreach (var e in enemies)
            {
                if (e == null) continue;
                var u = e.GetComponent<Unit>();
                if (u == null || u.hp <= 0) continue;
                targets.Add(e);
            }
        }

        if (targets.Count == 0) return;

        foreach (var go in targets)
        {
            if (go == null) continue;
            var st = go.GetComponent<UnitStatusEffectController>();
            // 프로젝트에 ApplyDebuff가 따로 있으면 그걸로 바꾸세요:
            // st?.ApplyDebuff(debuff);
            st?.ApplyBuff(debuff);
        }
    }
}