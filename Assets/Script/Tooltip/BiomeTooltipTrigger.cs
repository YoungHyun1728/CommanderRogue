using UnityEngine;
using UnityEngine.EventSystems;

public class BiomeTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] RunManager run;
    [SerializeField] BiomeTooltipDatabase db;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (run == null || db == null || TooltipSystem.Instance == null) return;

        var biome = run.CurrentBiome;
        string title = BiomeText.ToDisplayName(biome);
        string body = db.GetDesc(biome);
        string effect = db.GetEffect(biome);

        TooltipSystem.Instance.Show(title, body, effect);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }
}