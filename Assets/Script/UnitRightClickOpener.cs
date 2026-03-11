using UnityEngine;

public class UnitClickOpener : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask unitLayer = ~0; // 유닛 레이어로 제한하고 싶으면 설정

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (cam == null) return;

        Vector2 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);

        // 2D 콜라이더 위치
        Collider2D col = Physics2D.OverlapPoint(worldPos, unitLayer);

        if (col != null)
        {
            Unit unit = col.GetComponentInParent<Unit>();
            if (unit != null)
            {
                UnitInfoUIManager.Instance.Open(unit); // 고정 위치 UI
                return;
            }
        }
    }
}