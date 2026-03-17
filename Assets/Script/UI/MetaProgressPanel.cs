using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메타 강화 선택 리스트(아이콘/이름/레벨)와 상세 패널을 묶어주는 스크립트.
/// </summary>
public class MetaProgressPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text currencyText;

    [Serializable]
    private class UpgradeListItem
    {
        public MetaUpgradeType type;
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text levelText;
        public Button selectButton;
    }

    [SerializeField] private List<UpgradeListItem> listItems = new();

    [Header("Detail Panel")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailLevelText;
    [SerializeField] private TMP_Text detailTotalBonusText;
    [SerializeField] private TMP_Text detailPerLevelText;
    [SerializeField] private TMP_Text detailCostText;
    [SerializeField] private TMP_Text detailCurrencyText;
    [SerializeField] private TMP_Text detailDescText;
    [SerializeField] private Button buyButton;

    private MetaUpgradeType selectedType = MetaUpgradeType.StartGold;

    void Awake()
    {
        WireButtons();
        WireBuyButton();
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
        foreach (var item in listItems)
        {
            if (item == null || item.selectButton == null) continue;
            var captured = item.type;
            item.selectButton.onClick.AddListener(() => Select(captured));
        }
    }

    private void WireBuyButton()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(BuySelected);
    }

    private void Select(MetaUpgradeType type)
    {
        selectedType = type;
        RefreshDetail();
    }

    private void BuySelected()
    {
        var mgr = MetaProgressManager.Instance;
        if (mgr == null) return;

        bool success = mgr.TryPurchase(selectedType, out _, out _);
        ToastManager.Instance?.Show(success ? "강화 완료!" : "스코어가 부족합니다.", 0.6f, 0.2f);
        Refresh();
    }

    public void Refresh()
    {
        var mgr = MetaProgressManager.Instance;
        if (mgr == null) return;

        if (currencyText != null)
            currencyText.text = Math.Floor(mgr.Currency).ToString("N0");

        RefreshList();
        RefreshDetail();
    }

    private void RefreshList()
    {
        var mgr = MetaProgressManager.Instance;
        var perLevel = mgr.GetUnitBonusPerLevel();
        int goldPerLv = mgr.GetStartGoldPerLevel();

        foreach (var item in listItems)
        {
            if (item == null) continue;

            int level = mgr.GetLevel(item.type);

            if (item.nameText != null)
                item.nameText.text = GetDisplayName(item.type);

            if (item.levelText != null)
                item.levelText.text = $"Lv.{level}";
        }

        // 선택된 항목이 없으면 첫 항목 선택
        if (listItems.Count > 0 && !HasListType(selectedType))
            selectedType = listItems[0].type;
    }

    private bool HasListType(MetaUpgradeType type)
    {
        foreach (var item in listItems)
        {
            if (item != null && item.type == type) return true;
        }
        return false;
    }

    private void RefreshDetail()
    {
        var mgr = MetaProgressManager.Instance;
        if (mgr == null) return;

        var perLevel = mgr.GetUnitBonusPerLevel();
        int goldPerLv = mgr.GetStartGoldPerLevel();

        int level = mgr.GetLevel(selectedType);
        bool isMax = mgr.IsMaxLevel(selectedType);
        double cost = mgr.GetNextCost(selectedType);

        var info = GetBonusInfo(selectedType, perLevel, goldPerLv, level);

        if (detailIcon != null)
            detailIcon.sprite = FindIcon(selectedType);

        if (detailNameText != null)
            detailNameText.text = GetDisplayName(selectedType);

        if (detailLevelText != null)
            detailLevelText.text = $"Lv.{level}";

        if (detailTotalBonusText != null)
            detailTotalBonusText.text = $"현재 총 보너스: {FormatValue(info.total, info.unit, info.format)}";

        if (detailPerLevelText != null)
            detailPerLevelText.text = $"강화당 증가량: {FormatValue(info.per, info.unit, info.format)}";

        if (detailCostText != null)
            detailCostText.text = isMax ? "강화비용: MAX" : $"강화비용: {Mathf.RoundToInt((float)cost):N0} Score";

        if (detailCurrencyText != null)
            detailCurrencyText.text = $"보유 Score: {Math.Floor(mgr.Currency):N0}";

        if (detailDescText != null)
            detailDescText.text = GetDescription(selectedType);

        if (buyButton != null)
            buyButton.interactable = !isMax && mgr.Currency >= cost;
    }

    private (double per, double total, string unit, string format) GetBonusInfo(MetaUpgradeType type, MetaUnitStatBonus perLevel, int goldPerLv, int level)
    {
        level = Mathf.Max(0, level);
        switch (type)
        {
            case MetaUpgradeType.StartGold:
                return (goldPerLv, goldPerLv * level, "Gold", "N0");
            case MetaUpgradeType.BaseMaxHp:
                return (perLevel.baseMaxHp, perLevel.baseMaxHp * level, "HP", "F0");
            case MetaUpgradeType.BaseAttackDamage:
                return (perLevel.baseAttackDamage, perLevel.baseAttackDamage * level, "ATK", "F1");
            case MetaUpgradeType.BaseAttackSpeed:
                return (perLevel.baseAttackSpeed, perLevel.baseAttackSpeed * level, "APS", "F3");
            case MetaUpgradeType.BaseMpRecovery:
                return (perLevel.baseMpRecovery, perLevel.baseMpRecovery * level, "MP 회복", "F2");
            case MetaUpgradeType.Strength:
                return (perLevel.strength, perLevel.strength * level, "STR", "F1");
            case MetaUpgradeType.Agility:
                return (perLevel.agility, perLevel.agility * level, "AGI", "F1");
            case MetaUpgradeType.Intelligence:
                return (perLevel.intelligence, perLevel.intelligence * level, "INT", "F1");
        }
        return (0, 0, "", "F1");
    }

    private string FormatValue(double value, string unit, string format)
    {
        string number = value.ToString(format);
        return string.IsNullOrEmpty(unit) ? number : $"{number} {unit}";
    }

    private string GetDisplayName(MetaUpgradeType type)
    {
        return type switch
        {
            MetaUpgradeType.StartGold => "시작 골드 증가",
            MetaUpgradeType.BaseMaxHp => "기본 체력 증가",
            MetaUpgradeType.BaseAttackDamage => "기본 공격력 증가",
            MetaUpgradeType.BaseAttackSpeed => "기본 공속 증가",
            MetaUpgradeType.BaseMpRecovery => "기본 마나회복 증가",
            MetaUpgradeType.Strength => "힘 스탯 증가",
            MetaUpgradeType.Agility => "민첩 스탯 증가",
            MetaUpgradeType.Intelligence => "지능 스탯 증가",
            _ => type.ToString()
        };
    }

    private string GetDescription(MetaUpgradeType type)
    {
        return type switch
        {
            MetaUpgradeType.StartGold => "게임 시작 시 추가 골드를 지급합니다.",
            MetaUpgradeType.BaseMaxHp => "모든 플레이어 유닛의 기본 체력이 증가합니다.",
            MetaUpgradeType.BaseAttackDamage => "모든 플레이어 유닛의 기본 공격력이 증가합니다.",
            MetaUpgradeType.BaseAttackSpeed => "모든 플레이어 유닛의 기본 공격속도가 증가합니다.",
            MetaUpgradeType.BaseMpRecovery => "모든 플레이어 유닛의 마나 회복량이 증가합니다.",
            MetaUpgradeType.Strength => "모든 플레이어 유닛의 힘 스탯이 증가합니다.",
            MetaUpgradeType.Agility => "모든 플레이어 유닛의 민첩 스탯이 증가합니다.",
            MetaUpgradeType.Intelligence => "모든 플레이어 유닛의 지능 스탯이 증가합니다.",
            _ => string.Empty
        };
    }

    private Sprite FindIcon(MetaUpgradeType type)
    {
        foreach (var item in listItems)
        {
            if (item != null && item.type == type && item.icon != null)
                return item.icon.sprite;
        }
        return null;
    }
}
