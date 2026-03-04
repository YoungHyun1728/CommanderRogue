using UnityEngine;
using UnityEngine.EventSystems;

public class EquipmentListTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private EquipmentTooltipUI tooltipUI;
    [SerializeField] private Vector2 fixedAnchoredPosition;

    private Unit _unit;

    public void SetContext(Unit unit) // UnitInfoPanel이 매 프레임 갱신해줄 수 있음
    {
        _unit = unit;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipUI == null || _unit == null) return;       
        tooltipUI.ShowList(_unit.equippedItems, fixedAnchoredPosition);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipUI?.Hide();
    }
    
}