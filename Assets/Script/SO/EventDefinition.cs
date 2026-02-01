using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Game/Event Definition")]
public class EventDefinition : ScriptableObject
{
    public string eventId;          
    public string title;
    [TextArea] public string description;
    
    [Range(0, 100)] public int weight = 10; // 가중치

    public List<EventChoice> choices = new List<EventChoice>();
}

[Serializable]
public class EventChoice
{
    public string buttonText;

    // 예시 효과들(필요한 것만 추가해가면 됨)
    public int goldCost;

    public bool startBanditBattle;      // 도적단 전투 같은 이벤트 전투
    public int banditPresetKey;         // EnemySpawnManager.SpawnBanditBattle(key)용
    public bool healPartyFull;          // 간단 회복 이벤트
    public bool changeWeatherRandom;    // 날씨 랜덤 변경
    public bool leave;                  // 그냥 지나가기
}
