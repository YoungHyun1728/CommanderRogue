using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitCardView : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI unitSummary;
    [SerializeField] private Button selectButton;

    private UnitData data;
    private Action<UnitData> onClick;

    public void Setup(UnitData data, Action<UnitData> onClick)
    {
        this.data = data;
        this.onClick = onClick;

        portrait.sprite = data.portrait;
        nameText.text = data.unitName;
        unitSummary.text = data.unitSummary;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() =>
        {
            this.onClick?.Invoke(this.data);
        });
    }

}
