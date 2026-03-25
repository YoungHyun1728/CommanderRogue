using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보상/상점/리롤/보상 적용(유닛 대상 포함) 흐름을 담당한다.
/// RunManager는 라운드 상태 전환과 외부 공개 API만 유지한다.
/// </summary>
public sealed class RunRewardCoordinator
{
    private readonly RunManager run;

    public RunRewardCoordinator(RunManager run)
    {
        this.run = run;
    }

    public void EnterRewardFromEvent(string overrideEventId = null)
    {
        if (!string.IsNullOrEmpty(overrideEventId))
            run.currentEventId = overrideEventId;

        run.isInReward = true;
        run.rerollCountThisRound = 0;
        run.currentRunState = RunState.Reward;
        GiveReward();
    }

    public void EnterShopOnlyFromLeave()
    {
        run.isInReward = true;
        run.rerollCountThisRound = 0;
        run.currentRunState = RunState.Reward;
        GiveReward(forcedRewardCount: 0);
    }

    public void EnterReward()
    {
        run.isInReward = true;
        run.rerollCountThisRound = 0;
        run.currentRunState = RunState.Reward;
        GiveReward();
    }

    public int GetRerollCost()
    {
        int round = (run.currentLevel == 0) ? 1 : run.currentLevel;
        int baseCost = round * run.RerollBaseCostPerRound;
        int multiplier = 1 + run.rerollCountThisRound * run.RerollCostStep;
        return baseCost * multiplier;
    }

    public void OnSkipReward()
    {
        run.pendingReward = null;
        FinishPending();

        run.isInReward = false;
        run.RewardPhasePanel.gameObject.SetActive(false);
        run.GoToNextRound();
    }

    public void GiveReward(int forcedRewardCount = -1)
    {
        int rewardCount = (forcedRewardCount >= 0) ? forcedRewardCount : GetRewardCount();

        var rewardChoices = run.RewardManager.GetRewardChoices(
            run.currentLevel, rewardCount,
            run.currentNodeType, run.currentEventId,
            forceGlobalPool: false
        ) ?? new List<RewardDefinition>();

        var shopChoices = run.RewardManager.GetShopItems(run.currentLevel) ?? new List<RewardDefinition>();

        shopChoices.RemoveAll(r =>
            r != null &&
            r.rewardType == RewardType.GainUnit &&
            run.playerUnits.Count >= 10
        );

        run.RewardPhasePanel.Open(
            rewardChoices,
            shopChoices,
            OnRewardSelected,
            OnShopItemClicked,
            GetRerollCost,
            OnReroll,
            OnSkipReward
        );

        run.RewardPhasePanel.gameObject.SetActive(true);
    }

    public int GetRewardCount()
    {
        if (run.currentLevel >= 145) return 6;
        if (run.currentLevel >= 95) return 5;
        if (run.currentLevel >= 55) return 4;
        if (run.currentLevel >= 25) return 3;
        return 2;
    }

    public void OnRewardSelected(RewardDefinition reward)
    {
        if (run.pendingReward != null) return;
        run.pendingReward = reward;
        run.pendingIsShop = false;
        run.pendingWasFreeReward = true;
        run.pendingShopCost = 0;

        HandleRewardPick(reward);
    }

    public void OnShopItemClicked(RewardDefinition reward)
    {
        if (run.pendingReward != null) return;

        int cost = GetShopPrice(reward);
        if (run.gold < cost)
        {
            ToastManager.Instance?.Show("골드가 부족합니다.", 0.4f, 0.2f);
            return;
        }

        run.pendingReward = reward;
        run.pendingIsShop = true;
        run.pendingWasFreeReward = false;
        run.pendingShopCost = cost;

        HandleRewardPick(reward);
    }

    public void CommitPendingPurchaseIfNeeded()
    {
        if (!run.pendingIsShop) return;
        run.gold -= run.pendingShopCost;
    }

    public void FinishPending()
    {
        run.pendingReward = null;
        run.pendingIsShop = false;
        run.pendingShopCost = 0;
    }

    public void HandleRewardPick(RewardDefinition reward)
    {
        run.pendingReward = reward;

        if (reward.targetType == RewardTargetType.None)
        {
            CommitPendingPurchaseIfNeeded();
            ApplyRewardNoTarget(reward);
            AfterPurchaseSideEffectsIfNeeded(reward);
            FinishRewardFlow();
            return;
        }

        if (reward.targetType == RewardTargetType.RandomUnit)
        {
            CommitPendingPurchaseIfNeeded();
            ApplyRewardToRandomUnit(reward);
            AfterPurchaseSideEffectsIfNeeded(reward);
            FinishRewardFlow();
            return;
        }

        OpenEquipToUnitUI(reward);
    }

    public void FinishRewardFlow()
    {
        run.pendingReward = null;

        if (run.pendingWasFreeReward)
        {
            FinishPending();
            run.isInReward = false;
            run.RewardPhasePanel.gameObject.SetActive(false);
            run.GoToNextRound();
        }
        else
        {
            FinishPending();
            run.RewardPhasePanel.gameObject.SetActive(true);
        }
    }

    public void AfterPurchaseSideEffectsIfNeeded(RewardDefinition reward)
    {
        if (run.pendingIsShop && reward.rewardType == RewardType.GainUnit)
            run.GatherHeroBuyCount++;

        run.RewardPhasePanel?.RefreshShopPrices();
    }

    public int GetShopPrice(RewardDefinition r)
    {
        int round = run.currentLevel;
        float price = r.baseShopPrice;

        if (r.scaleWithRound)
        {
            float mul = Mathf.Pow(r.roundPriceMultiplier, Mathf.Max(0, round - 1));
            price *= mul;
        }

        if (r.scaleWithPurchaseCount)
            price += r.pricePerPurchase * run.GatherHeroBuyCount;

        return Mathf.Max(0, Mathf.RoundToInt(price));
    }

    public int GetGoldAmount(RewardDefinition r)
    {
        int round = run.currentLevel;
        float goldAmount = r.goldAmount;

        if (r.scaleWithRound)
        {
            float mul = Mathf.Pow(r.roundPriceMultiplier, Mathf.Max(0, round - 1));
            goldAmount *= mul;
        }

        float relicMul = 1f + 0.25f * run.goldAmulet;
        goldAmount *= relicMul;

        return Mathf.Max(0, Mathf.RoundToInt(goldAmount));
    }

    public int GetScaledGoldAmount(float baseGold, bool scaleWithRound, float roundMultiplier)
    {
        float goldAmount = baseGold;

        if (scaleWithRound)
        {
            int round = run.currentLevel;
            float mul = Mathf.Pow(roundMultiplier, Mathf.Max(0, round - 1));
            goldAmount *= mul;
        }

        float relicMul = 1f + 0.25f * run.goldAmulet;
        goldAmount *= relicMul;
        return Mathf.Max(0, Mathf.RoundToInt(goldAmount));
    }

    public void OnRewardClicked(RewardDefinition reward)
    {
        switch (reward.targetType)
        {
            case RewardTargetType.None:
                ApplyRewardNoTarget(reward);
                break;
            case RewardTargetType.ChooseUnit:
                OpenEquipToUnitUI(reward);
                break;
            case RewardTargetType.RandomUnit:
                ApplyRewardToRandomUnit(reward);
                break;
        }
    }

    public void ApplyRewardNoTarget(RewardDefinition reward)
    {
        switch (reward.rewardType)
        {
            case RewardType.Gold:
                run.gold += GetGoldAmount(reward);
                break;

            case RewardType.InstantHeal:
                foreach (var unitGO in run.playerUnits)
                {
                    var unit = unitGO.GetComponent<Unit>();
                    if (unit == null) continue;
                    unit.HealByPotion(reward.healAmount, reward.healProportion, reward.fullHeal);
                }
                break;

            case RewardType.InstantExp:
                var expTargets = new List<GameObject>(run.playerUnits);
                foreach (var unitGO in expTargets)
                {
                    var unit = unitGO.GetComponent<Unit>();
                    if (unit == null) continue;
                    unit.GainLevel(reward.levelIncrease + run.levelPotionBonus);
                }
                break;

            case RewardType.Relic:
                run.levelPotionBonus += reward.levelPotionBonus;
                run.expAmulet += reward.expAmulet;
                run.goldAmulet += reward.goldAmulet;
                break;

            case RewardType.Revive:
                foreach (var unitGO in run.playerUnits)
                {
                    var unitfsm = unitGO.GetComponent<UnitFSM>();
                    if (unitfsm == null) continue;
                    unitfsm.ReviveToEmptyTile(false);
                }
                break;

            case RewardType.GainUnit:
                run.GainUnit();
                break;

            default:
                Debug.LogWarning($"RewardType {reward.rewardType} 는 처리되지 않았습니다.");
                break;
        }
    }

    public void OpenEquipToUnitUI(RewardDefinition reward)
    {
        var unitList = new List<Unit>();
        foreach (var unitGO in run.playerUnits)
        {
            var u = unitGO.GetComponent<Unit>();
            if (u != null) unitList.Add(u);
        }

        run.RewardPhasePanel.gameObject.SetActive(false);
        run.ChooseUnitPanel.Open(
            unitList,
            selectedUnit =>
            {
                CommitPendingPurchaseIfNeeded();
                ApplyRewardToUnit(reward, selectedUnit);
                AfterPurchaseSideEffectsIfNeeded(reward);
                FinishRewardFlow();
            },
            () =>
            {
                run.pendingReward = null;
                FinishPending();
                run.RewardPhasePanel.gameObject.SetActive(true);
            });
    }

    public void ApplyRewardToRandomUnit(RewardDefinition reward)
    {
        if (run.playerUnits.Count == 0)
        {
            Debug.LogWarning("플레이어 유닛이 없어 무작위 보상을 적용할 수 없습니다.");
            return;
        }

        int idx = Random.Range(0, run.playerUnits.Count);
        GameObject targetGO = run.playerUnits[idx];
        Unit target = targetGO.GetComponent<Unit>();

        if (target == null)
        {
            Debug.LogWarning("랜덤 대상에 Unit 컴포넌트가 없습니다.");
            return;
        }

        ApplyRewardToUnit(reward, target);
    }

    public void ApplyRewardToUnit(RewardDefinition reward, Unit unit)
    {
        switch (reward.rewardType)
        {
            case RewardType.Equipment:
                unit.Equip(reward.equipment);
                break;
            case RewardType.InstantHeal:
                unit.HealByPotion(reward.healAmount, reward.healProportion, reward.fullHeal);
                break;
            case RewardType.InstantExp:
                unit.GainLevel(reward.levelIncrease + run.levelPotionBonus);
                break;
            case RewardType.PassiveItem:
                unit.AddPassiveItem(reward);
                break;
            case RewardType.Revive:
                var unitfsm = unit.GetComponent<UnitFSM>();
                if (unitfsm != null) unitfsm.ReviveToEmptyTile(reward.reviveHerb);
                break;
            default:
                Debug.LogWarning($"RewardType {reward.rewardType} 은 Unit 대상 보상으로 처리되지 않습니다.");
                break;
        }
    }

    public void OnReroll()
    {
        int cost = GetRerollCost();
        if (run.gold < cost)
        {
            ToastManager.Instance?.Show("골드가 부족합니다.", 0.5f, 0.2f);
            return;
        }

        run.gold -= cost;
        run.rerollCountThisRound++;

        int rewardCount = GetRewardCount();
        var rewardChoices = run.RewardManager.GetRewardChoices(
            run.currentLevel, rewardCount,
            run.currentNodeType, run.currentEventId,
            forceGlobalPool: true
        );

        var shopChoices = run.RewardManager.GetShopItems(run.currentLevel);

        run.RewardPhasePanel.Open(
            rewardChoices,
            shopChoices,
            OnRewardSelected,
            OnShopItemClicked,
            GetRerollCost,
            OnReroll,
            OnSkipReward
        );
    }
}
