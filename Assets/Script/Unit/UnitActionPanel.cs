using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitActionPanel : MonoBehaviour
{
    [SerializeField] private Button upButton;
    [SerializeField] private Button downButton;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button closeButton;

    [Header("정보 표시용")]
    //[SerializeField] private TextMeshProUGUI nameText;
    //[SerializeField] private TextMeshProUGUI hpText;

    [Header("위치 이동")]
    [SerializeField] private Canvas canvas;          // 이 패널이 달려 있는 캔버스
    [SerializeField] private Camera battleCamera;    // 타일맵을 보는 카메라
    [SerializeField] private Vector2 screenOffset;

    private UnitFSM currentUnit;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        gameObject.SetActive(false);

        upButton.onClick.AddListener(() => OnClickMove(new Vector2Int(0, 1)));
        downButton.onClick.AddListener(() => OnClickMove(new Vector2Int(0, -1)));
        leftButton.onClick.AddListener(() => OnClickMove(new Vector2Int(-1, 0)));
        rightButton.onClick.AddListener(() => OnClickMove(new Vector2Int(1, 0)));

        closeButton.onClick.AddListener(Close);
    }

    public void Open(UnitFSM unit)
    {
        currentUnit = unit;
        RefreshInfo();
        UpdatePosition();
        gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        // 열려 있고, 유닛이 있으면 계속 따라다니게
        if (gameObject.activeSelf && currentUnit != null)
        {
            UpdatePosition();
        }
    }

    private void RefreshInfo()
    {
        if (currentUnit == null) return;

        Unit unitComp = currentUnit.GetComponent<Unit>();
        if (unitComp != null)
        {
            //nameText.text = unitComp.unitName;
            //hpText.text = $"{unitComp.hp}/{unitComp.maxHp}";
        }
    }

    private void UpdatePosition()
    {
        if (currentUnit == null || canvas == null) return;

        // 1) 유닛 월드 좌표
        Vector3 worldPos = currentUnit.transform.position;

        // 2) 사용할 카메라 (배틀카메라 우선)
        Camera cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null) return;

        // 3) 월드 → 스크린 좌표 (픽셀)
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // 4) 스크린 좌표 → 캔버스 로컬 좌표
        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localPos
        );

        // 5) 살짝 오프셋 더해서 패널 위치 세팅
        rectTransform.anchoredPosition = localPos + screenOffset;
    }

    private void OnClickMove(Vector2Int delta)
    {
        if (currentUnit == null) return;

        bool moved = currentUnit.TryMoveBy(delta);

        if (!moved)
        {
            // 이동 실패 (벽/다른 유닛/범위 밖 등)
            Debug.Log("해당 방향으로 이동할 수 없습니다.");
        }
        else
        {
            // 이동 후 정보 갱신 (위치 / 스탯 등)
            RefreshInfo();
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
        currentUnit = null;
    }
}
