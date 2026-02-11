using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [SerializeField] private List<EventDefinition> eventPool; // 이벤트 SO 리스트
    [SerializeField] private EventPanel eventPanel;           // UI 패널(아래에서 만들 거)
    public int GetPreviewGoldCost(EventChoice choice) => GetActualGoldCost(choice);
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 등급별로 랜덤 뽑기
    public string PickRandomEventId()
    {
        var pickedRarity = RollRarity();
        var list = eventPool.FindAll(e => e != null && e.rarity == pickedRarity);

        // 해당 등급 이벤트가 비어있으면 Common으로 폴백(또는 다음 등급으로 폴백도 가능)
        if (list.Count == 0)
            list = eventPool.FindAll(e => e != null && e.rarity == EventRarity.Common);

        return PickWeighted(list);
    }

    // 등급 뽑기
    private EventRarity RollRarity()
    {
        // 0~1 실수
        float r = Random.value;

        // 전설 1%, 유니크 3%, 희귀 20%, 나머지 일반
        if (r < 0.01f) return EventRarity.Legendary;
        if (r < 0.01f + 0.03f) return EventRarity.Unique;
        if (r < 0.01f + 0.03f + 0.20f) return EventRarity.Rare;
        return EventRarity.Common;
    }

    // 가중치로 뽑기
    private string PickWeighted(List<EventDefinition> list)
    {
        int total = 0;
        foreach (var e in list) total += Mathf.Max(0, e.weight);
        if (total <= 0) return list.Count > 0 ? list[0].eventId : "";

        int r = Random.Range(0, total);
        int acc = 0;
        foreach (var e in list)
        {
            acc += Mathf.Max(0, e.weight);
            if (r < acc) return e.eventId;
        }
        return list[0].eventId;
    }

    // ID로 이벤트 정의 가져오기
    public EventDefinition GetById(string id)
    {
        return eventPool.Find(e => e != null && e.eventId == id);
    }

    // 이벤트 시작
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

    // 실제 골드 비용 계산 (스케일링 포함)
    private int GetActualGoldCost(EventChoice choice)
    {
        if (choice.useScaledGoldCost)
        {
            // 라운드의 진행에 따라 비용 증가  
            int round = RunManager.Instance.currentLevel;
            return Mathf.Max(0, choice.baseGoldCost + choice.goldCostPerRound * Mathf.Max(0, round - 1));
        }

        return Mathf.Max(0, choice.baseGoldCost);
    }

    // 플레이어가 선택지 고름
    void OnChoiceSelected(EventDefinition def, EventChoice choice)
    {
        // 골드 체크
        int cost = GetActualGoldCost(choice);

        if (cost > 0 && RunManager.Instance.gold < cost)
        {
            ToastManager.Instance?.Show("골드가 부족합니다.");
            return;
        }

        RunManager.Instance.gold -= cost;

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

        if (choice.startQuest && choice.questToStart != null)
        {
            QuestManager.Instance?.StartQuest(choice.questToStart);
        }

        // 이벤트 종료 후 다음 라운드
        eventPanel.Close();
        RunManager.Instance.GoToNextRound();
    }
}
