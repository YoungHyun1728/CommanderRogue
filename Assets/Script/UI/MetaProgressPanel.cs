using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI helper for spending Score on meta upgrades.
/// Wire each row in the inspector to show level/cost and trigger purchases.
/// </summary>
public class MetaProgressPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text currencyText;

    [Serializable]
    private class UpgradeRow
    {
        public MetaUpgradeType type;
        public TMP_Text levelText;
        public TMP_Text costText;
        public TMP_Text bonusText;
        public Button buyButton;
    }

    [SerializeField] private List<UpgradeRow> rows = new();

    void Awake()
    {
        WireButtons();
    }

    void OnEnable()
    {
        Refresh();
    }

    public void Toggle(bool show)
    {
        if (root != null) root.SetActive(show);
        if (show) Refresh();
    }

    private void WireButtons()
    {
        foreach (var row in rows)
        {
            if (row == null || row.buyButton == null) continue;
            var captured = row.type;
            row.buyButton.onClick.AddListener(() => HandlePurchase(captured));
        }
    }

    private void HandlePurchase(MetaUpgradeType type)
    {
        var mgr = MetaProgressManager.Instance;
        if (mgr == null) return;

        bool success = mgr.TryPurchase(type, out var cost, out var _);
        ToastManager.Instance?.Show(success ? "강화 완료!" : "스코어가 부족합니다.", 0.6f, 0.2f);
        Refresh();
    }

    public void Refresh()
    {
        var mgr = MetaProgressManager.Instance;
        if (mgr == null) return;

        if (currencyText != null)
            currencyText.text = Math.Floor(mgr.Currency).ToString("N0");

        var perLevel = mgr.GetUnitBonusPerLevel();
        int goldPerLv = mgr.GetStartGoldPerLevel();

        foreach (var row in rows)
        {
            if (row == null) continue;

            int level = mgr.GetLevel(row.type);
            bool isMax = mgr.IsMaxLevel(row.type);
            double cost = mgr.GetNextCost(row.type);

            if (row.levelText != null)
                row.levelText.text = $"Lv.{level}";

            if (row.costText != null)
                row.costText.text = isMax ? "MAX" : $"{Mathf.RoundToInt((float)cost)} Score";

            if (row.buyButton != null)
                row.buyButton.interactable = !isMax && mgr.Currency >= cost;

            if (row.bonusText != null)
                row.bonusText.text = BuildBonusText(row.type, perLevel, goldPerLv, level);
        }
    }

    private string BuildBonusText(MetaUpgradeType type, MetaUnitStatBonus perLevel, int goldPerLv, int level)
    {
        level = Mathf.Max(0, level);
        switch (type)
        {
            case MetaUpgradeType.StartGold:
                return $"총 +{goldPerLv * level:N0} Gold";
            case MetaUpgradeType.BaseMaxHp:
                return $"총 +{perLevel.baseMaxHp * level:F0} HP";
            case MetaUpgradeType.BaseAttackDamage:
                return $"총 +{perLevel.baseAttackDamage * level:F1} ATK";
            case MetaUpgradeType.BaseAttackSpeed:
                return $"총 +{perLevel.baseAttackSpeed * level:F3} APS";
            case MetaUpgradeType.Strength:
                return $"총 +{perLevel.strength * level:F1} STR";
            case MetaUpgradeType.Agility:
                return $"총 +{perLevel.agility * level:F1} AGI";
            case MetaUpgradeType.Intelligence:
                return $"총 +{perLevel.intelligence * level:F1} INT";
        }
        return string.Empty;
    }
}
