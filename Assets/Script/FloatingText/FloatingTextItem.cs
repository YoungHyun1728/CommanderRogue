using System;
using TMPro;
using UnityEngine;

public class FloatingTextItem : MonoBehaviour
{
    [SerializeField] private TMP_Text tmp;
    [SerializeField] private CanvasGroup canvasGroup; // 있으면 페이드 깔끔

    private FloatingTextPoolManager pool;
    public Action OnDone;

    public float SpawnTime { get; private set; }
    public bool IsAlive { get; private set; }

    private Vector3 startScreenPos;
    private Vector2 startAnchoredPos;
    private float elapsed;
    private FloatingTextStyle style;

    private float extraPush; // 스택으로 밀린 누적값

    public void SetPool(FloatingTextPoolManager p) => pool = p;

    void Reset()
    {
        tmp = GetComponentInChildren<TMP_Text>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Play(Vector3 screenPos, string text, FloatingTextStyle st)
    {
        SpawnTime = Time.time;
        IsAlive = true;

        elapsed = 0f;
        style = st;
        extraPush = 0f;

        tmp.text = text;
        tmp.color = st.color;
        tmp.fontSize = st.fontSize;

        transform.localScale = Vector3.one * st.startScale;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        RectTransform rt = (RectTransform)transform;
        RectTransform parentRt = (RectTransform)rt.parent;

        Camera cam = null;
        var canvas = parentRt.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt,
            screenPos,
            cam,
            out startAnchoredPos
        );

        rt.anchoredPosition = startAnchoredPos;
    }

    public void PushUp(float pixels)
    {
        startAnchoredPos += new Vector2(0f, pixels);
        var rt = (RectTransform)transform;
        rt.anchoredPosition = startAnchoredPos;
    }

    void Update()
    {
        if (!IsAlive) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, style.duration));

        RectTransform rt = (RectTransform)transform;

        // 위치 상승
        float rise = Mathf.Lerp(0f, style.risePixels, t);
        rt.anchoredPosition = startAnchoredPos + new Vector2(0f, rise);

        // 스케일
        float sc = Mathf.Lerp(style.startScale, style.endScale, t);
        rt.localScale = Vector3.one * sc;

        // 페이드
        if (style.useFade)
        {
            float a = 1f - t;
            if (canvasGroup != null) canvasGroup.alpha = a;
            else
            {
                var c = tmp.color;
                c.a = a;
                tmp.color = c;
            }
        }

        if (t >= 1f)
        {
            IsAlive = false;
            OnDone?.Invoke();
            OnDone = null;
        }
    }

}
