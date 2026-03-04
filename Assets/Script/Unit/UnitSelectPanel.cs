using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UnitSelectPanel : MonoBehaviour
{
    [SerializeField] private Transform cardParent;
    [SerializeField] private UnitCardView cardPrefab;
    [SerializeField] private GameObject hoverSummaryRoot;   // UI under the cards
    [SerializeField] private TextMeshProUGUI hoverSummaryText;

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
        if (hoverSummaryText != null) hoverSummaryText.text = data.unitSummary;
    }

    private void HideSummary()
    {
        //if (hoverSummaryRoot != null) hoverSummaryRoot.SetActive(false);
        if (hoverSummaryText != null) hoverSummaryText.text = string.Empty;
    }
}