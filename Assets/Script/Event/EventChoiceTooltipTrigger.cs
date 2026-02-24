using UnityEngine;
using UnityEngine.EventSystems;

public class EventChoiceTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private EventPanel _panel;
    private EventChoice _choice;

    public void Bind(EventPanel panel, EventChoice choice)
    {
        _panel = panel;
        _choice = choice;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _panel?.ShowChoiceTooltip(_choice);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _panel?.HideChoiceTooltip();
    }
}