using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentTooltipLine : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text text;

    public void Set(Sprite icon, string line)
    {
        if (iconImage) iconImage.sprite = icon;
        if (text) text.text = line;
    }
}