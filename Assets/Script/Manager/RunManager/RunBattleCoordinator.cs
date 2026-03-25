using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 시작/종료/판정과 전투 보상 분배를 담당한다.
/// RunManager는 상태 전환의 진입점만 유지하고, 전투 상세 로직은 이 클래스로 위임한다.
/// </summary>
public sealed class RunBattleCoordinator
{
    private readonly RunManager run;

    public RunBattleCoordinator(RunManager run)
    {
        this.run = run;
    }

    /// <summary>
    /// Ready 상태에서 전투를 시작한다.
    /// </summary>
    public void StartBattle()
    {
        if (run.currentRunState != RunState.Ready)
            return;

        if (run.currentNodeType == NodeType.Boss)
            run.PlayBossBattleBgm();

        run.SavePlayerFormation();
        SyncUnitsToTiles(run.playerUnits);
        SyncUnitsToTiles(run.enemyUnits);

        run.TileMapManager.RebuildOccupancyFromUnits(run.playerUnits, run.enemyUnits);
        run.isInBattle = true;
        run.currentRunState = RunState.Battle;
        run.RefreshFleeButton();

        run.ResetBattleFlagsForAllUnits();

        bool hasPartyStun = run.HasPendingPartyStunAtBattleStart();
        if (hasPartyStun)
        {
            run.ApplyPendingPartyDebuffs();
            run.EnemyUnitsIdle();
        }
        else
        {
            run.AllUnitsIdle();
            run.ApplyPendingPartyDebuffs();
        }

        run.biomeEffects?.ApplyBattleStartEffects();
    }

    /// <summary>
    /// 적 유닛 처치 처리: 보상 풀 누적/골드 지급/유닛 제거/종료 판정.
    /// </summary>
    public void OnEnemyDefeated(GameObject enemyGO)
    {
        if (run.isRunTerminated) return;
        if (enemyGO == null) return;

        var enemyUnit = enemyGO.GetComponent<Unit>();
        if (enemyUnit != null)
        {
            double baseReward = run.GetRequiredExp(enemyUnit.level) * run.EnemyExpFraction;
            run.battleExpPool += baseReward;

            double relicMul = 1.0 + 0.25 * run.goldAmulet;
            double baseGold = enemyUnit.level * run.EnemyGoldCoefficient + relicMul;
            run.gold += (int)baseGold;
        }

        run.enemyUnits.Remove(enemyGO);
        if (run.TileMapManager != null) run.TileMapManager.enemyUnits.Remove(enemyGO);

        run.totalEnemyKills++;
        Object.Destroy(enemyGO);

        CheckEndBattle();
    }

    /// <summary>
    /// 전투 종료 조건(적 전멸/아군 전멸)을 검사한다.
    /// </summary>
    public void CheckEndBattle()
    {
        if (!run.isInBattle || run.isRunTerminated) return;

        if (!HasAliveEnemyUnit())
        {
            run.isInBattle = false;
            EndBattle(true);
        }
        else if (!HasAlivePlayerUnit())
        {
            run.isInBattle = false;
            EndBattle(false);
        }
    }

    private void EndBattle(bool isWin)
    {
        if (isWin)
        {
            AwardBattleExpToParty();
            run.ReviveAndHealPartyFull();
            run.RestorePlayerFormation();
            run.biomeEffects?.ApplyBattleEndEffects();

            if (run.currentLevel == 200 || run.currentLevel == 250)
            {
                run.ShowGameClearUI();
                return;
            }

            run.EnterReward();
            return;
        }

        run.ShowGameOverUI();
    }

    private void AwardBattleExpToParty()
    {
        if (run.battleExpPool <= 0) return;

        var receivers = new List<Unit>();
        foreach (var go in run.playerUnits)
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            if (u.hp <= 0) continue;
            receivers.Add(u);
        }

        if (receivers.Count == 0) return;

        double per = run.battleExpPool / receivers.Count;
        double relicMul = 1.0 + 0.25 * run.expAmulet;

        foreach (var u in receivers)
            u.GainExp(per * relicMul);

        run.battleExpPool = 0;
    }

    private static void SyncUnitsToTiles(List<GameObject> units)
    {
        foreach (var go in units)
        {
            if (go == null || !go.activeInHierarchy) continue;
            var fsm = go.GetComponent<UnitFSM>();
            var agent = go.GetComponent<UnitGridAgent>();
            if (fsm != null && agent != null)
                agent.ForceSyncToTile(fsm.currentTilePosition);
        }
    }

    private bool HasAlivePlayerUnit()
    {
        foreach (var go in run.playerUnits)
        {
            if (go == null) continue;
            var unit = go.GetComponent<Unit>();
            if (unit != null && unit.hp > 0)
                return true;
        }

        return false;
    }

    private bool HasAliveEnemyUnit()
    {
        foreach (var go in run.enemyUnits)
        {
            if (go == null) continue;
            var unit = go.GetComponent<Unit>();
            if (unit != null && unit.hp > 0)
                return true;
        }

        return false;
    }
}
