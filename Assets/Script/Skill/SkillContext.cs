using System.Collections.Generic;
using UnityEngine;

public struct SkillContext
{
    public Unit caster; 
    public UnitFSM casterFsm; 
    public UnitSkillSystem casterSkills; 
    public UnitStatusEffectController casterStatus; 

    public TileMapManager tileMap; 
    public RunManager run;

    // 스킬 실행 시 추가로 넘겨야 하는 값(예: 실제 피해량)
    public double param;

    public GameObject targetGO;
    public Unit targetUnit;
    public UnitFSM targetFsm;

    public SkillContext(Unit caster, UnitFSM casterFsm, UnitSkillSystem skills, UnitStatusEffectController status, GameObject targetGO)
    {
        this.caster = caster;
        this.casterFsm = casterFsm;
        this.casterSkills = skills;
        this.casterStatus = status;

        this.tileMap = Object.FindObjectOfType<TileMapManager>();
        this.run = RunManager.Instance;

        this.param = 0;

        this.targetGO = targetGO;
        this.targetUnit = targetGO ? targetGO.GetComponent<Unit>() : null;
        this.targetFsm = targetGO ? targetGO.GetComponent<UnitFSM>() : null;
    }

    public List<GameObject> GetAllies() 
    {
        // 태그 기준: PlayerUnit이면 RunManager.playerUnits, EnemyUnit이면 RunManager.enemyUnits
        if (casterFsm != null && casterFsm.CompareTag("EnemyUnit"))
            return run.enemyUnits;
        return run.playerUnits;
    }

    public List<GameObject> GetEnemies() 
    {
        if (casterFsm != null && casterFsm.CompareTag("EnemyUnit"))
            return run.playerUnits;
        return run.enemyUnits;
    }


    // ===== Targeting helpers =====

    public List<Unit> GetAlliedUnits(bool includeSelf = true)
    {
        var list = new List<Unit>();
        foreach (var go in GetAllies())
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            if (!includeSelf && go == caster.gameObject) continue;
            list.Add(u);
        }
        return list;
    }

    public List<Unit> GetEnemyUnits()
    {
        var list = new List<Unit>();
        foreach (var go in GetEnemies())
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            list.Add(u);
        }
        return list;
    }

    // 2) 인접 8칸의 적 유닛만
    public List<Unit> GetAdjacentEnemies8()
    {
        var result = new List<Unit>();
        if (casterFsm == null) return result;

        Vector2Int c = casterFsm.currentTilePosition;

        foreach (var go in GetEnemies())
        {
            if (go == null) continue;
            var enemyFsm = go.GetComponent<UnitFSM>();
            var enemyUnit = go.GetComponent<Unit>();
            if (enemyFsm == null || enemyUnit == null) continue;

            Vector2Int p = enemyFsm.currentTilePosition;
            int dx = Mathf.Abs(p.x - c.x);
            int dy = Mathf.Abs(p.y - c.y);

            // 8방향 이웃(자기 자신 제외)
            if ((dx <= 1 && dy <= 1) && !(dx == 0 && dy == 0))
                result.Add(enemyUnit);
        }
        return result;
    }

    // 5) 현재 타겟 1명 + n명(거리 가까운 순)
    public List<Unit> GetCurrentPlusNEnemies(int extraCount)
    {
        var result = new List<Unit>();

        if (targetUnit != null)
            result.Add(targetUnit);

        if (casterFsm == null) return result;

        Vector2Int c = casterFsm.currentTilePosition;

        var candidates = new List<Unit>();
        foreach (var go in GetEnemies())
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            if (u == targetUnit) continue;
            if (u.hp <= 0) continue;
            candidates.Add(u);
        }

        candidates.Sort((a, b) =>
        {
            var af = a.GetComponent<UnitFSM>();
            var bf = b.GetComponent<UnitFSM>();
            if (af == null || bf == null) return 0;
            int ad = Mathf.Abs(af.currentTilePosition.x - c.x) + Mathf.Abs(af.currentTilePosition.y - c.y);
            int bd = Mathf.Abs(bf.currentTilePosition.x - c.x) + Mathf.Abs(bf.currentTilePosition.y - c.y);
            return ad.CompareTo(bd);
        });

        int take = Mathf.Max(0, extraCount);
        for (int i = 0; i < candidates.Count && i < take; i++)
            result.Add(candidates[i]);

        return result;
    }

}