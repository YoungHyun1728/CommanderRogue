using UnityEngine;

// 커서의 움직임(단순 위아래 움직임)
public class NodeCursor : MonoBehaviour
{
    [SerializeField] private float amplitude = 10f; // 위아래 이동량 (px)
    [SerializeField] private float speed = 2f;      // 움직임 속도

    private RectTransform rect;
    private Vector2 basePos;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        basePos = rect.anchoredPosition;
    }

    void OnEnable()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        basePos = rect.anchoredPosition; // 노드 따라 부모 바뀐 뒤 기준점 다시 잡기
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * amplitude;
        rect.anchoredPosition = basePos + new Vector2(0f, offset);
    }

    public void SetBaseFromCurrent()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();
        basePos = rect.anchoredPosition;
    }
}
