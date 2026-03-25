using UnityEngine;

/// <summary>
/// 런 상태 직렬화/역직렬화와 타이틀 복귀 저장 정책을 담당한다.
/// 파일 I/O는 SaveManager가 담당하고, 이 클래스는 런 오브젝트 <-> SaveData 변환만 담당한다.
/// </summary>
public sealed class RunSaveCoordinator
{
    private readonly RunManager run;

    public RunSaveCoordinator(RunManager run)
    {
        this.run = run;
    }

    /// <summary>
    /// 맵 복귀 시점 자동 저장을 수행한다.
    /// </summary>
    public void GoToNextRound()
    {
        run.MapGenerator.MapViewOn();
        run.currentRunState = RunState.OnMap;

        var snapshot = BuildSaveData(run.MapGenerator);
        SaveManager.instance?.SaveGame(snapshot);
    }

    public SaveData BuildSaveData(MapGenerator mapGen)
    {
        var data = new SaveData
        {
            currentLevel = run.currentLevel,
            gold = run.gold,
            currentBiome = run.CurrentBiome,
            rerollCountThisRound = run.rerollCountThisRound,
            totalEnemyKills = run.totalEnemyKills,
            nextBattleEnemyLevelOffset = run.nextBattleEnemyLevelOffset,
            levelPotionBonus = run.levelPotionBonus,
            expAmulet = run.expAmulet,
            goldAmulet = run.goldAmulet
        };

        data.pendingDebuffs.Clear();
        foreach (var debuff in run.pendingPartyDebuffs)
        {
            data.pendingDebuffs.Add(new PendingDebuffState
            {
                type = debuff.type,
                duration = debuff.duration,
                dpsRatioOfMaxHp = debuff.dpsRatioOfMaxHp,
                multiplier = debuff.multiplier
            });
        }

        data.playerUnits.Clear();
        foreach (var go in run.playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            var unit = go.GetComponent<Unit>();
            if (fsm == null || unit == null) continue;

            data.playerUnits.Add(new PlayerUnitState
            {
                unitDataName = !string.IsNullOrEmpty(unit.originUnitDataName) ? unit.originUnitDataName : unit.unitName,
                level = unit.level,
                exp = unit.exp,
                hp = unit.hp,
                mp = unit.mp,
                tileX = fsm.currentTilePosition.x,
                tileY = fsm.currentTilePosition.y,
                isAlive = unit.hp > 0
            });

            var savedUnit = data.playerUnits[^1];
            foreach (var eq in unit.equippedItems)
            {
                if (eq == null) continue;
                string name = !string.IsNullOrEmpty(eq.itemName) ? eq.itemName : eq.name;
                savedUnit.equippedItemNames.Add(name);
            }
        }

        if (mapGen != null)
        {
            mapGen.EnsureCurrentNode(run.currentLevel);
            Debug.Log("[RunManager] BuildSaveData -> FillSaveData(Map)");
            mapGen.FillSaveData(data);
        }

        data.isValid = true;
        return data;
    }

    public void RestoreRun(SaveData data)
    {
        if (data == null || !data.isValid)
        {
            run.StartNewRun();
            return;
        }

        Time.timeScale = 1f;
        run.isRunTerminated = false;
        run.isInBattle = false;
        run.isInEvent = false;
        run.isInReward = false;

        run.totalEnemyKills = data.totalEnemyKills;
        run.currentLevel = data.currentLevel;
        run.gold = data.gold;
        run.CurrentBiome = data.currentBiome;
        run.nextBattleEnemyLevelOffset = data.nextBattleEnemyLevelOffset;
        run.levelPotionBonus = data.levelPotionBonus;
        run.expAmulet = data.expAmulet;
        run.goldAmulet = data.goldAmulet;

        run.EnsureLevelUpExpTable();
        run.battleExpPool = 0;
        run.battleGoldPool = 0;
        run.currentRunState = RunState.OnMap;

        run.pendingPartyDebuffs.Clear();
        foreach (var debuff in data.pendingDebuffs)
        {
            run.pendingPartyDebuffs.Add(new RunManager.PendingPartyDebuff
            {
                type = debuff.type,
                duration = debuff.duration,
                dpsRatioOfMaxHp = debuff.dpsRatioOfMaxHp,
                multiplier = debuff.multiplier
            });
        }

        foreach (var go in run.enemyUnits)
        {
            if (go != null) Object.Destroy(go);
        }
        run.enemyUnits.Clear();
        run.TileMapManager.enemyUnits.Clear();

        foreach (var go in run.playerUnits)
        {
            if (go != null) Object.Destroy(go);
        }
        run.playerUnits.Clear();
        run.TileMapManager.playerUnits.Clear();

        foreach (var pu in data.playerUnits)
        {
            if (string.IsNullOrEmpty(pu.unitDataName)) continue;

            var baseData = run.PlayerUnitPool.Find(u => u != null && (u.name == pu.unitDataName || u.unitName == pu.unitDataName));
            if (baseData == null) continue;

            UnitData spawnData = pu.level >= 100 ? run.ResolveAwakenedData(baseData) : baseData;
            spawnData.level = Mathf.Max(1, pu.level);
            spawnData.isPlayerUnit = true;

            var unitGO = run.SpawnUnitAtTile(spawnData, new Vector2Int(pu.tileX, pu.tileY));
            var unitComp = unitGO.GetComponent<Unit>();
            if (unitComp == null) continue;

            unitComp.level = spawnData.level;
            unitComp.exp = pu.exp;
            unitComp.RefreshStats();
            unitComp.hp = System.Math.Max(0, System.Math.Min(pu.hp, unitComp.maxHp));
            unitComp.mp = Mathf.Clamp(pu.mp, 0, unitComp.maxMp);
            if (!pu.isAlive) unitComp.hp = 0;

            if (pu.equippedItemNames != null)
            {
                foreach (var eqName in pu.equippedItemNames)
                {
                    var eq = run.ResolveEquipmentByName(eqName);
                    if (eq != null) unitComp.Equip(eq);
                }
            }
        }

        run.TileMapManager.RebuildOccupancyFromUnits(run.playerUnits, run.enemyUnits);
        run.biomeEffects?.ApplyPersistentToParty(run.CurrentBiome);
        run.MapGenerator?.MapViewOn();

        if (SaveManager.instance != null)
        {
            SaveManager.instance.loadRequested = false;
            SaveManager.pendingAutoLoad = false;
        }
    }

    /// <summary>
    /// 설정창 등에서 호출되는 타이틀 복귀 처리.
    /// 현재 정책은 "라운드 종료 자동 저장만 사용, 추가 저장 생략"이다.
    /// </summary>
    public void SaveAndReturnToTitle()
    {
        Debug.Log("[RunManager] SaveAndReturnToTitle: skip save, just go title");
        Time.timeScale = 1f;
        run.GoToTitleScene();
    }
}
