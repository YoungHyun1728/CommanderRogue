using UnityEngine;
using System.Collections.Generic;

public class ChooseUnitPanel : MonoBehaviour
{
    [SerializeField] private Transform gridParent;
    [SerializeField] private UnitButtonUI unitButtonPrefab;
    [SerializeField] private UnityEngine.UI.Button backButton;
    
    private System.Action<Unit> onSelect;
    private System.Action onBack;

    public void Open(List<Unit> units, System.Action<Unit> callback, System.Action onBack)
    {        
        onSelect = callback;
        this.onBack = onBack;

        foreach (Transform child in gridParent)
            Destroy(child.gameObject);

        foreach (var unit in units)
        {
            var btn = Instantiate(unitButtonPrefab, gridParent);
            btn.Setup(unit, () => OnClickUnit(unit));
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                this.onBack?.Invoke();
            });
        }

        gameObject.SetActive(true);
    }

    private void OnClickUnit(Unit unit)
    {
        gameObject.SetActive(false);
        onSelect?.Invoke(unit);
    }
}
