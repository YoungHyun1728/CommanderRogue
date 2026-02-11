using System;
using System.Collections.Generic;
using UnityEngine;

public enum EventRarity
{
    Common,
    Rare,
    Unique,
    Legendary
}

[CreateAssetMenu(menuName="Game/Event Definition")]
public class EventDefinition : ScriptableObject
{
    public string eventId;          
    public string title;
    [TextArea] public string description;
    public EventRarity rarity = EventRarity.Common;    
    [Range(0, 100)] public int weight = 10; // 가중치
    public List<EventChoice> choices = new List<EventChoice>();
}

[Serializable]
public class EventChoice
{
    public string buttonText;

    // 선택지 구성(필요한 것만 추가해가면 됨)
    public bool useScaledGoldCost; // 라운드 스케일링 사용 여부
    public int baseGoldCost;      // 기본 비용
    public int goldCostPerRound; // 라운드당 추가 비용

    public bool startBanditBattle;      // 도적단 전투 같은 이벤트 전투
    public int banditPresetKey;         // EnemySpawnManager.SpawnBanditBattle(key)용
    public bool healPartyFull;          // 간단 회복 이벤트
    public bool changeWeatherRandom;    // 날씨 랜덤 변경
    public bool leave;                  // 그냥 지나가기

    // 고블린 상인 호위 퀘스트
    public bool startQuest;
    public QuestDefinition questToStart;
}
