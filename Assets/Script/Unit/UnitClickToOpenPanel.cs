using UnityEngine;

public class UnitClickToOpenPanel : MonoBehaviour
{
    [SerializeField] private UnitActionPanel actionPanel;
    private UnitFSM unitFSM;

    private void Awake()
    {
        unitFSM = GetComponent<UnitFSM>();

        if (actionPanel == null)
        {
            actionPanel = FindObjectOfType<UnitActionPanel>(true);
            if (actionPanel == null)
            {
                Debug.LogError("UnitActionPanel을 씬에서 찾을 수 없습니다!");
            }
        }
    }

    private void OnMouseDown()
    {
        if (unitFSM.CurrentState != UnitFSM.UnitState.Ready)
            return;

        actionPanel.Open(unitFSM);
    }
}
