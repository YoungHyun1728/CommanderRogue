using System.Collections.Generic;
using UnityEngine;
public class FloatingTextPoolManager : MonoBehaviour
{
    public static FloatingTextPoolManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private Canvas canvas;                 // FloatingTextCanvas
    [SerializeField] private FloatingTextItem prefab;       // FloatingTextPrefab
    [SerializeField] private int prewarm = 50;
    [SerializeField] private Camera worldCamera;

    [Header("Stacking")]
    [SerializeField] private float stackPushPixels = 5f;   // 새 텍스트 뜰 때 기존 텍스트를 위로 미는 정도
    [SerializeField] private float stackWindow = 0.9f;      // 이 시간 안에 뜬 텍스트끼리만 같은 스택으로 취급
    [SerializeField] public FloatingTextStyleSet styleSet;
    public FloatingTextStyleSet Styles => styleSet;


    private readonly Queue<FloatingTextItem> pool = new();
    private readonly Dictionary<int, List<FloatingTextItem>> activeByAnchor = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        for (int i = 0; i < prewarm; i++)
        {
            var it = CreateNew();
            Return(it);
        }
    }

    private FloatingTextItem CreateNew() 
    {
        var it = Instantiate(prefab, canvas.transform);
        it.gameObject.SetActive(false);
        it.SetPool(this);
        return it;
    }

    private FloatingTextItem Get()
    {
        if (pool.Count > 0) return pool.Dequeue();
        return CreateNew();
    }

    public void Return(FloatingTextItem item)
    {
        item.gameObject.SetActive(false);
        item.transform.SetParent(canvas.transform);
        pool.Enqueue(item);
    }

    // ====== 외부에서 쓰는 API ======
    public void Show(Transform worldAnchor, string text, FloatingTextStyle style, Vector3 worldOffset)
    {
        if (worldAnchor == null) return;

        // 월드 -> 스크린
        Vector3 worldPos = worldAnchor.position + worldOffset;

        // (0~1) 카메라 뷰포트 좌표
        Vector3 vp = worldCamera.WorldToViewportPoint(worldPos);

        // 카메라가 실제로 출력하는 화면 픽셀 영역
        Rect pr = worldCamera.pixelRect;
        
        Vector3 screen = new Vector3(
            pr.x + vp.x * pr.width,
            pr.y + vp.y * pr.height,
            vp.z
        );

        var item = Get();
        item.transform.SetParent(canvas.transform);
        item.gameObject.SetActive(true);

        int anchorId = worldAnchor.GetInstanceID();
        if (!activeByAnchor.TryGetValue(anchorId, out var list))
        {
            list = new List<FloatingTextItem>();
            activeByAnchor[anchorId] = list;
        }

        // 스택 정리
        float now = Time.time;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null || !list[i].IsAlive || (now - list[i].SpawnTime) > stackWindow)
                list.RemoveAt(i);
        }

        // 기존 텍스트 밀어내기
        for (int i = 0; i < list.Count; i++)
        {
            list[i].PushUp(stackPushPixels);
        }

        // 새 텍스트 시작
        item.Play(screen, text, style);

        // 등록
        list.Add(item);

        // 완료시 제거 콜백
        item.OnDone = () =>
        {
            if (activeByAnchor.TryGetValue(anchorId, out var l))
                l.Remove(item);

            Return(item);
        };
    }

    public void ShowDamage(Transform anchor, double amount, Vector3 worldOffset)
    {
        Show(anchor, $"-{amount}", styleSet.damage, worldOffset);
    }

    public void ShowHeal(Transform anchor, double amount, Vector3 worldOffset)
    {
        Show(anchor, $"+{amount}", styleSet.heal, worldOffset);
    }

    public void ShowStatus(Transform anchor, string text, Vector3 worldOffset)
    {
        Show(anchor, text, styleSet.status, worldOffset);
    }

    public void ShowSpeech(Transform anchor, string text, Vector3 worldOffset)
    {
        Show(anchor, text, styleSet.speech, worldOffset);
    }

    public void ShowSystem(Transform anchor, string text, Vector3 worldOffset)
    {
        Show(anchor, text, styleSet.system, worldOffset);
    }

}

[System.Serializable]
public struct FloatingTextStyle
{
    public float duration;
    public float risePixels;      // 위로 올라가는 총량(픽셀)
    public float startScale;
    public float endScale;
    public bool useFade;
    public float fontSize;
    public Color color;
}


