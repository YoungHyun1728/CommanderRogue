using UnityEngine;
using UnityEngine.EventSystems;

public class StatTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] [SerializeField] private string mainStat;
    [TextArea] [SerializeField] private string subStat1;
    [TextArea] [SerializeField] private string sunStat2;

    public void OnPointerEnter(PointerEventData eventData)
    {
        TooltipSystem.Instance.Show(TooltipChannel.Stat, mainStat, subStat1, sunStat2);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance.Hide(TooltipChannel.Stat);
    }
}