using TMPro;
using UnityEngine;

public class TooltipUI : MonoBehaviour
{
    [SerializeField] RectTransform root;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] TMP_Text effectText;
    Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        Hide();
    }

    public void Show(string title, string body, string effect)
    {
        titleText.text = title;
        bodyText.text = body;
        effectText.text = effect;

        root.gameObject.SetActive(true);
    }

    public void Hide()
    {
        root.gameObject.SetActive(false);
    }
}