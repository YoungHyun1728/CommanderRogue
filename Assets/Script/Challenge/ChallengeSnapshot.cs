using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 챌린지 모드에 업로드할 파티 스냅샷 데이터 컨테이너.
/// PlayFab Title Data / CloudScript 전송용으로 직렬화 가능해야 하므로
/// Unity 직렬화 친화적인 POCO 형태로 유지한다.
/// </summary>
[Serializable]
public class ChallengePartySnapshot
{
    public string partyId;            // 고유 파티 ID (GUID)
    public string creatorId;          // 클라이언트 고유 식별자 (디바이스/플레이어)
    public string partyName;          // 유저 지정 이름
    public int round;                 // 클리어 라운드
    public double partyPower;         // 파티 전투력 합산
    public string createdAtIsoUtc;    // 생성 시각(UTC ISO8601)
    public List<ChallengeUnitSnapshot> units = new List<ChallengeUnitSnapshot>();
}

[Serializable]
public class ChallengeUnitSnapshot
{
    public string unitDataName;
    public string displayName;
    public int level;
    public double hp;
    public double maxHp;
    public float mp;
    public float maxMp;
    public int tileX;
    public int tileY;
    public bool isAlive;
    public List<string> equippedItemNames = new List<string>();

    // 전투력/스탯 요약(미러 생성 시 참고)
    public double totalStrength;
    public double totalAgility;
    public double totalIntelligence;
    public double attackDamage;
    public float attackSpeed;
    public float moveSpeed;
}
