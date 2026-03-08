using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UnitSelectPanel : MonoBehaviour
{
    [SerializeField] private Transform cardParent;
    [SerializeField] private UnitCardView cardPrefab;
    [SerializeField] private GameObject hoverSummaryRoot;   // UI under the cards

    [Header("요약 텍스트")]
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private TextMeshProUGUI strengthText;
    [SerializeField] private TextMeshProUGUI agilityText;
    [SerializeField] private TextMeshProUGUI intelligenceText;
    [SerializeField] private TextMeshProUGUI attackRangeText;
    [SerializeField] private TextMeshProUGUI baseAttackText;
    [SerializeField] private TextMeshProUGUI baseHpText;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillDescText;

    private Action<UnitData> onSelected;

    public void Open(List<UnitData> candidates, Action<UnitData> onSelected)
    {
        gameObject.SetActive(true);
        this.onSelected = onSelected;
        foreach (Transform child in cardParent) Destroy(child.gameObject);

        HideSummary();

        foreach (var data in candidates)
        {
            var card = Instantiate(cardPrefab, cardParent);
            card.Setup(data, OnClickCard, ShowSummary, HideSummary);
        }
    }

    private void OnClickCard(UnitData data)
    {
        gameObject.SetActive(false);
        onSelected?.Invoke(data);
    }

    private void ShowSummary(UnitData data)
    {
        //if (hoverSummaryRoot != null) hoverSummaryRoot.SetActive(true);

        summaryText?.SetText(data.unitSummary ?? string.Empty);
        strengthText?.SetText($"{data.strength:0.##} (+{data.strengthPerLevel:0.##})");
        agilityText?.SetText($"{data.agility:0.##} (+{data.agilityPerLevel:0.##})");
        intelligenceText?.SetText($"{data.intelligence:0.##} (+{data.intelligencePerLevel:0.##})");
        attackRangeText?.SetText($"{data.attackRange} 칸");
        baseAttackText?.SetText($"{data.baseAttackDamage:0.##}");
        baseHpText?.SetText($"{data.baseMaxHp:0.##}");

        if (data.fullManaSkill != null)
        {
            skillNameText?.SetText(data.fullManaSkill.displayName);
            skillDescText?.SetText(data.fullManaSkillDescription ?? string.Empty);
        }
        else
        {
            skillNameText?.SetText(string.Empty);
            skillDescText?.SetText(string.Empty);
        }
    }

    private void HideSummary()
    {
        //if (hoverSummaryRoot != null) hoverSummaryRoot.SetActive(false);
        summaryText?.SetText(string.Empty);
        strengthText?.SetText(string.Empty);
        agilityText?.SetText(string.Empty);
        intelligenceText?.SetText(string.Empty);
        attackRangeText?.SetText(string.Empty);
        baseAttackText?.SetText(string.Empty);
        baseHpText?.SetText(string.Empty);
        skillNameText?.SetText(string.Empty);
        skillDescText?.SetText(string.Empty);
    }
}
