using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]

public class NodeData
{
    public int level;                // 노드가 속한 레벨
    public int index;                // 노드의 인덱스 (레벨 내에서의 위치)
    public NodeType type;            // 노드의 타입 (전투, 휴식 등)
    public List<int> connectedIndices;  // 연결된 노드들의 인덱스

    // 런 진행 상태
    public bool isClicked;
    public bool isCurrent;
    public bool isResolved;
    public string resolvedEventId;
}

[System.Serializable]
public class PlayerUnitState
{
    public string unitDataName; // UnitData.asset 이름 (원본 ID)
    public int level;
    public double exp;
    public double hp;
    public float mp;
    public int tileX;
    public int tileY;
    public bool isAlive;
    public List<string> equippedItemNames = new List<string>();
}

[System.Serializable]
public class PendingDebuffState
{
    public RunManager.PendingPartyDebuff.Type type;
    public float duration;
    public float dpsRatioOfMaxHp;
    public float multiplier;
}

public class SaveData
{
    public List<NodeData> mapNodes = new List<NodeData>(); // 노드 데이터 리스트
    public int currentLevel; // 현재 레벨
    public int currentNodeLevel;
    public int currentNodeIndex;
    public int gold;        // 플레이어가 가지고 있는 골드
    public BiomeType currentBiome;

    public int rerollCountThisRound;
    public int totalEnemyKills;
    public int nextBattleEnemyLevelOffset;

    public int levelPotionBonus;
    public int expAmulet;
    public int goldAmulet;

    public List<PlayerUnitState> playerUnits = new List<PlayerUnitState>();
    public List<PendingDebuffState> pendingDebuffs = new List<PendingDebuffState>();

    public bool isValid;
}
