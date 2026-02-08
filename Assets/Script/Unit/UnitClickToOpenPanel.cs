using UnityEngine;

// 유닛 이동을 위한 조작UI 활성화
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
        if (unitFSM.CurrentState != UnitFSM.UnitState.Ready 
            && RunManager.Instance.currentRunState != RunState.Ready)
            return;

        actionPanel.Open(unitFSM);
    }
}
