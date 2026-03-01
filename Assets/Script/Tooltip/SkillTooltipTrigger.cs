using UnityEngine;
using UnityEngine.EventSystems;

public class SkillTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Unit unit;
    private SkillDefinition skill;

    public void SetContext(Unit u, SkillDefinition s)
    {
        unit = u;
        skill = s;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (skill == null) return;

        TooltipSystem.Instance.HideAll();
        string body = skill.BuildRuntimeDescription(unit);
        TooltipSystem.Instance.Show(TooltipChannel.UnitSkill, skill.displayName, body, "");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance.Hide(TooltipChannel.UnitSkill);
    }
}