using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [SerializeField] private List<EventDefinition> eventPool; // 이벤트 SO 리스트
    [SerializeField] private EventPanel eventPanel;           // UI 패널(아래에서 만들 거)

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public string PickRandomEventId()
    {
        int total = 0;
        foreach (var e in eventPool) total += Mathf.Max(0, e.weight);
        if (total <= 0) return eventPool.Count > 0 ? eventPool[0].eventId : "";

        int r = Random.Range(0, total);
        int acc = 0;
        foreach (var e in eventPool)
        {
            acc += Mathf.Max(0, e.weight);
            if (r < acc) return e.eventId;
        }
        return eventPool[0].eventId;
    }

    public EventDefinition GetById(string id)
    {
        return eventPool.Find(e => e != null && e.eventId == id);
    }

    public void StartEvent(string eventId)
    {
        Debug.Log($"[StartEvent] try eventId = '{eventId}'");
        var def = GetById(eventId);
        if (def == null)
        {
            Debug.LogWarning($"EventDefinition not found: {eventId}");
            RunManager.Instance.GoToNextRound();
            return;
        }

        //RunManager.Instance.currentRunState = RunState.Event;
        eventPanel.Open(def, OnChoiceSelected);
    }

    void OnChoiceSelected(EventDefinition def, EventChoice choice)
    {
        // 골드 체크
        if (choice.goldCost > 0 && RunManager.Instance.gold < choice.goldCost)
        {
            ToastManager.Instance?.Show("골드가 부족합니다.");
            return;
        }

        RunManager.Instance.gold -= choice.goldCost;

        // 회복 이벤트
        if (choice.healPartyFull)
        {
            foreach (var go in RunManager.Instance.playerUnits)
            {
                if (go == null) continue;
                var u = go.GetComponent<Unit>();
                if (u == null) continue;
                u.HealByPotion(0, 0, true);
            }
            ToastManager.Instance?.Show("파티가 회복되었습니다.");
        }

        if (choice.startBanditBattle)
        {
            // 이벤트 전투 시작: 너는 이미 SpawnBanditBattle 준비돼 있음 
            // 여기서 Ready/Battle 흐름으로 붙이는 방식은 너 전투 진입 로직에 맞춰 연결하면 됨.
            // 예: RunManager에 EnterReadyEventBattle 같은 함수 만들어 호출 추천.
            eventPanel.Close();
            RunManager.Instance.StartEventBanditBattle(choice.banditPresetKey);
            return;
        }

        // 그냥 지나가기/기타 효과
        if (choice.leave)
        {
            ToastManager.Instance?.Show("아무 일도 일어나지 않았다...");
        }

        // 이벤트 종료 후 다음 라운드
        eventPanel.Close();
        RunManager.Instance.GoToNextRound();
    }
}
