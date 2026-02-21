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

public enum EventRollType
{
    None,

    // 확률
    RandomChance,

    // 파티 스탯 합산 판정
    PartyStrCheck,
    PartyAgiCheck,
    PartyIntCheck
}

public enum EventRepeatRule
{
    Unlimited,      // 여러 번 가능
    OncePerRun,     // 런 전체에서 1회만
    MaxTimesPerRun  // 런 전체에서 N회까지
}

[Serializable]
public class EventOutcome
{
    [Header("확률/조건")]
    public EventRollType rollType = EventRollType.None;
    [Range(0f, 1f)]
    public float chance = 1f; // RandomChance일 때 사용

    // Party*Check일 때: 라운드 * multiplier 를 기준으로 판정
    public int statMultiplier = 0;

    // 보상 풀을 이벤트 id로 덮어쓰기(예: Strevent1/2/기본 등)
    public string rewardEventIdOverride;

    [Header("분기")]
    [Tooltip("비어있지 않으면, 보상 단계로 가지 않고 이 이벤트로 즉시 이어집니다.")]
    public string nextEventId;

    [Tooltip("true면 보상/리워드 단계를 건너뛰고 바로 다음 라운드로 진행(leave처럼 사용 가능).")]
    public bool skipRewardAndGoNextRound;
    
    [Header("즉시 효과 - HP/MP")]
    public bool healPartyFull; // 파티 full heal (hp clamp)
    public bool healPartyByMaxHpPercent;
    [Range(0f, 1f)] public float healMaxHpPercent;

    public bool damagePartyByCurrentHpPercent;
    [Range(0f, 1f)] public float damageCurrentHpPercent;

    public bool restorePartyManaTo100; // 100까지 회복 (maxMp에 맞춰 clamp)
    public bool restorePartyManaFlat;
    public int manaFlat;

    [Header("즉시 효과 - 부활")]
    public bool reviveAllFainted;  // 기절(비활성) 파티원을 부활

    [Header("다음 전투 상대 레벨")]
    public bool increaseNextBattleEnemyLevel;
    public int nextBattleEnemyLevelBonus = 1;

    [Header("다음 전투 패널티 - 파티 전체")]
    public bool addNextBattlePartyStun;
    public float stunDuration;

    public bool addNextBattlePartyPoison;
    public float poisonDuration;
    [Range(0f, 1f)] public float poisonDpsRatioOfMaxHp; // maxHp * ratio 를 초당 dps로

    public bool addNextBattlePartyBurnAmp;
    public float burnAmpMultiplier = 1.25f;
    public float burnAmpDuration = 5f;

    public bool addNextBattlePartyMoveSlow;
    public float moveSlowMultiplier = 0.7f; // 0~1, 작을수록 더 느림
    public float moveSlowDuration = 5f;

    public bool addNextBattlePartyAttackSlow;
    public float attackSlowMultiplier = 1.3f; // 1 이상, 클수록 공격 딜레이 증가
    public float attackSlowDuration = 5f;
}

[CreateAssetMenu(menuName = "Game/Event Definition")]
public class EventDefinition : ScriptableObject
{
    public string eventId;
    public string title;
    [TextArea] public string description;

    public EventRarity rarity = EventRarity.Common;
    [Range(0, 100)] public int weight = 10; // 가중치

    public List<EventChoice> choices = new List<EventChoice>();

    [Header("Spawn Rules")]
    public int minRound = 1;
    public int maxRound = 9999;

    // 비워두면 모든 바이옴 허용
    public List<BiomeType> allowedBiomes = new List<BiomeType>();

    public EventRepeatRule repeatRule = EventRepeatRule.Unlimited;
    public int maxTimesPerRun = 1;
}

[Serializable]
public class EventChoice
{
    public string buttonText;

    // 선택지 구성(필요한 것만 추가해가면 됨)
    public bool useScaledGoldCost; // 라운드 스케일링 사용 여부
    public int baseGoldCost;       // 기본 비용
    public float costMultiplier;   // 라운드당 추가 비용 곱해서 증가

    [Header("도적단 조우")]
    public bool startBanditBattle; // 도적단 전투 같은 이벤트 전투
    public int banditPresetKey;    // EnemySpawnManager.SpawnBanditBattle(key)용

    [Header("그 외 이벤트")]
    public bool healPartyFull;       // 간단 회복 이벤트(기본: 부활 X)
    public bool changeWeatherRandom; // 날씨 랜덤 변경
    public bool leave;               // 그냥 지나가기

    [Header("고블린 상인 호위")]
    public bool startQuest;
    public QuestDefinition questToStart;

    [Header("Outcomes")]
    public List<EventOutcome> outcomes = new List<EventOutcome>();
}