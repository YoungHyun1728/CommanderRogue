using UnityEngine;
using System.Collections.Generic;

public class ChooseUnitPanel : MonoBehaviour
{
    [SerializeField] private Transform gridParent;
    [SerializeField] private UnitButtonUI unitButtonPrefab;

    private System.Action<Unit> onSelect;

    public void Open(List<Unit> units, System.Action<Unit> callback)
    {
        onSelect = callback;

        foreach (Transform child in gridParent)
            Destroy(child.gameObject);

        foreach (var unit in units)
        {
            var btn = Instantiate(unitButtonPrefab, gridParent);
            btn.Setup(unit, () => OnClickUnit(unit));
        }

        gameObject.SetActive(true);
    }

    private void OnClickUnit(Unit unit)
    {
        gameObject.SetActive(false);
        onSelect?.Invoke(unit);
    }
}
