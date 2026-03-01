using UnityEngine;

public class UnitInfoUIManager : MonoBehaviour
{
    public static UnitInfoUIManager Instance { get; private set; }

    [SerializeField] private UnitInfoPanel panel; // 씬에 배치된 패널 참조

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    public void Open(Unit unit)
    {
        if (panel == null || unit == null) return;

        panel.gameObject.SetActive(true);
        panel.Bind(unit);
    }

    public void Close()
    {
        if (panel == null) return;
        panel.gameObject.SetActive(false);
    }
}