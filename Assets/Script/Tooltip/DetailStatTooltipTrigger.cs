using UnityEngine;
using UnityEngine.EventSystems;

public class DetailStatTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject TooltipUI;
    public void OnPointerEnter(PointerEventData eventData)
    {
        TooltipUI.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipUI.SetActive(false);
    }
}
