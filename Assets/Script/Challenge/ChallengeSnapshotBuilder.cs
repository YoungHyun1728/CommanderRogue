using System;
using UnityEngine;

/// <summary>
/// 현재 런 상태에서 챌린지 파티 스냅샷을 생성하는 헬퍼.
/// PlayFab 업로드/매칭 전에 호출한다.
/// </summary>
public static class ChallengeSnapshotBuilder
{
    public static ChallengePartySnapshot Build(RunManager runManager, string partyName)
    {
        if (runManager == null) throw new ArgumentNullException(nameof(runManager));

        var snapshot = new ChallengePartySnapshot
        {
            partyId = Guid.NewGuid().ToString("N"),
            creatorId = SystemInfo.deviceUniqueIdentifier,
            partyName = string.IsNullOrWhiteSpace(partyName) ? "Party" : partyName.Trim(),
            round = Mathf.Max(1, runManager.currentLevel),
            createdAtIsoUtc = DateTime.UtcNow.ToString("o"),
            partyPower = 0
        };

        foreach (var go in runManager.playerUnits)
        {
            if (go == null || !go.activeInHierarchy) continue;

            var unit = go.GetComponent<Unit>();
            var fsm = go.GetComponent<UnitFSM>();
            if (unit == null) continue;

            var unitSnap = new ChallengeUnitSnapshot
            {
                unitDataName = ResolveUnitDataName(unit),
                displayName = unit.unitName,
                level = unit.level,
                hp = unit.hp,
                maxHp = unit.maxHp,
                mp = unit.mp,
                maxMp = unit.maxMp,
                tileX = fsm != null ? fsm.currentTilePosition.x : 0,
                tileY = fsm != null ? fsm.currentTilePosition.y : 0,
                isAlive = unit.hp > 0,
                totalStrength = unit.totalStrength,
                totalAgility = unit.totalAgility,
                totalIntelligence = unit.totalIntelligence,
                attackDamage = unit.attackDamage,
                attackSpeed = unit.AttackSpeed,
                moveSpeed = unit.moveSpeed
            };

            if (unit.equippedItems != null)
            {
                foreach (var eq in unit.equippedItems)
                {
                    if (eq == null) continue;
                    unitSnap.equippedItemNames.Add(eq.name);
                }
            }

            snapshot.units.Add(unitSnap);
            snapshot.partyPower += unitSnap.totalStrength + unitSnap.totalAgility + unitSnap.totalIntelligence;
        }

        return snapshot;
    }

    private static string ResolveUnitDataName(Unit unit)
    {
        if (unit == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(unit.originUnitDataName))
            return unit.originUnitDataName;

        return unit.gameObject.name;
    }
}
