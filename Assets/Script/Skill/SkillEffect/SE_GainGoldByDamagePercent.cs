using UnityEngine;

[CreateAssetMenu(menuName="Game/SkillEffects/Gain Gold By Damage Percent")]
public class SE_GainGoldByDamagePercent : SkillEffectDefinition
{
    [Range(0f, 5f)]
    public float goldPercentOfDamage = 0.1f; // 예: 0.1 = 피해량의 10%

    public override void Execute(SkillContext ctx)
    {
        if (ctx.run == null) return;

        double dealt = ctx.param; // UnitSkillSystem.NotifyBasicAttackHit에서 넣어준 실제 피해량
        int addGold = Mathf.Max(0, Mathf.FloorToInt((float)(dealt * goldPercentOfDamage)));
        if (addGold <= 0) return;

        ctx.run.gold += addGold;
    }
}