using System;
using UnityEngine;

/// <summary>
/// 런 종료 시 결과 패널에 전달할 데이터 컨테이너.
/// </summary>
[Serializable]
public struct GameResultData
{
    public bool IsClear;
    public int Round;
    public int Gold;
    public int EnemyKills;
    public double PartyPower;
    public string TopUnitName;
    public Sprite TopUnitPortrait;
    public double Score;

    public GameResultData(
        bool isClear,
        int round,
        int gold,
        int enemyKills,
        double partyPower,
        string topUnitName,
        Sprite topUnitPortrait,
        double score)
    {
        IsClear = isClear;
        Round = round;
        Gold = gold;
        EnemyKills = enemyKills;
        PartyPower = partyPower;
        TopUnitName = topUnitName;
        TopUnitPortrait = topUnitPortrait;
        Score = score;
    }
}
