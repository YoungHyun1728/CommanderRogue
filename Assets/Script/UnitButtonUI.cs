using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitButtonUI : MonoBehaviour
{
    [SerializeField] private Image portraitImage;          // 유닛 이미지 있으면
    [SerializeField] private TextMeshProUGUI nameText;     // 유닛 이름
    [SerializeField] private TextMeshProUGUI level;     // 유닛 레벨
    [SerializeField] private Button button;                // 클릭용 버튼

    private Unit unit;
    private Action onClick;

    public void Setup(Unit unit, Action onClick)
    {
        this.unit = unit;
        this.onClick = onClick;

        if (nameText != null)
            nameText.text = unit.unitName;

        if (level != null) 
            level.text = unit.level.ToString();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            this.onClick?.Invoke();
        });
    }
}
