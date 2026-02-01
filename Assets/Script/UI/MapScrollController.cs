using UnityEngine;
using UnityEngine.UI;

public class MapScrollController : MonoBehaviour
{
    [Header("Assign")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Round Settings")]
    [SerializeField] private int maxRound = 200;

    // 게임에서 들고있는 현재 라운드 값을 여기로 넣어주면 됨
    [SerializeField] private int currentRound = 1;

    [Header("Options")]
    [SerializeField] private bool invert = false; // 방향 반대면 체크

    void Reset()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    void Awake()
    {
        if (!scrollRect) scrollRect = GetComponent<ScrollRect>();
    }

    void OnEnable()
    {
        currentRound = RunManager.Instance.CurrentLevel;
        // 비활성->활성 될 때마다 현재 라운드 위치로 이동
        ApplyRoundToScroll(currentRound);
    }

    public void SetCurrentRound(int round, bool moveScroll = true)
    {
        currentRound = RunManager.Instance.CurrentLevel;
        currentRound = Mathf.Clamp(round, 1, maxRound);
        if (moveScroll) ApplyRoundToScroll(currentRound);
    }

    public void ApplyRoundToScroll(int round)
    {
        round = Mathf.Clamp(round, 1, maxRound);

        float t = (maxRound <= 1) ? 0f : (round - 1f) / (maxRound - 1f);
        t = Mathf.Clamp01(t);
        if (invert) t = 1f - t;

        Canvas.ForceUpdateCanvases();
        scrollRect.horizontalNormalizedPosition = t;
    }

    // 버튼 누르고 있는 동안 연속 스크롤용: "초당 라운드 수" 개념으로 움직임
    public void ScrollByRounds(float deltaRounds)
    {
        if (maxRound <= 1) return;

        float deltaNorm = deltaRounds / (maxRound - 1f);
        float t = scrollRect.horizontalNormalizedPosition;
        t += invert ? -deltaNorm : deltaNorm;
        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(t);
    }

    // 버튼 뗄 때 가장 가까운 라운드로 스냅 + currentRound 업데이트
    public void SnapToNearestRound()
    {
        if (maxRound <= 1) return;
        currentRound = RunManager.Instance.CurrentLevel;
        float t = scrollRect.horizontalNormalizedPosition;
        if (invert) t = 1f - t;

        int round = Mathf.RoundToInt(t * (maxRound - 1f) + 1f);
        round = Mathf.Clamp(round, 1, maxRound);

        currentRound = round;
        ApplyRoundToScroll(currentRound);
    }

}