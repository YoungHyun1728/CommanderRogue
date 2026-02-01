using UnityEngine;

public class ContinueHintswwing : MonoBehaviour
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
    
    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * amplitude;
        rect.anchoredPosition = basePos + new Vector2(0f, offset);
    }
}
