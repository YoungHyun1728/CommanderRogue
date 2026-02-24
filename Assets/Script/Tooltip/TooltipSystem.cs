using UnityEngine;

public class TooltipSystem : MonoBehaviour
{
    public static TooltipSystem Instance { get; private set; }

    [SerializeField] TooltipUI ui;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show(string title, string body, string effect)
    {
        ui.Show(title, body, effect);
    }

    public void Hide()
    {
        ui.Hide();
    }
}