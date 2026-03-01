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

    // === Single target (호환용) ===
    public GameObject targetGO;
    public Unit targetUnit;
    public UnitFSM targetFsm;

    // === Multi targets (신규) ===
    // * 스킬 타겟이 여러 명인 경우 여기에 담아 사용하세요.
    // * 기존 코드 호환을 위해 targetGO/targetUnit/targetFsm 는 첫 번째 타겟을 가리킵니다.
    public List<GameObject> targetGOs;
    public List<Unit> targetUnits;
    public List<UnitFSM> targetFsms;

    public SkillContext(Unit caster, UnitFSM casterFsm, UnitSkillSystem skills, UnitStatusEffectController status, GameObject targetGO)
        : this(caster, casterFsm, skills, status, targetGO != null ? new List<GameObject> { targetGO } : null)
    {        
    }

    public SkillContext(Unit caster, UnitFSM casterFsm, UnitSkillSystem skills, UnitStatusEffectController status, List<GameObject> targetGOs)
    {
        this.caster = caster;
        this.casterFsm = casterFsm;
        this.casterSkills = skills;
        this.casterStatus = status;

        this.tileMap = Object.FindObjectOfType<TileMapManager>();
        this.run = RunManager.Instance;

        this.param = 0;

        // multi targets
        this.targetGOs = targetGOs ?? new List<GameObject>();
        this.targetUnits = new List<Unit>();
        this.targetFsms = new List<UnitFSM>();

        if (this.targetGOs != null)
        {
            for (int i = 0; i < this.targetGOs.Count; i++)
            {
                var go = this.targetGOs[i];
                if (go == null)
                {
                    this.targetUnits.Add(null);
                    this.targetFsms.Add(null);
                    continue;
                }

                this.targetUnits.Add(go.GetComponent<Unit>());
                this.targetFsms.Add(go.GetComponent<UnitFSM>());
            }
        }

        // single target (첫 번째)
        this.targetGO = (this.targetGOs != null && this.targetGOs.Count > 0) ? this.targetGOs[0] : null;
        this.targetUnit = this.targetGO ? this.targetGO.GetComponent<Unit>() : null;
        this.targetFsm = this.targetGO ? this.targetGO.GetComponent<UnitFSM>() : null;
    }

    // 여러 타겟이 있으면 그걸, 없으면 기존 단일 타겟을 반환
    public IEnumerable<GameObject> EnumerateTargets()
    {
        if (targetGOs != null && targetGOs.Count > 0)
        {
            for (int i = 0; i < targetGOs.Count; i++)
                if (targetGOs[i] != null)
                    yield return targetGOs[i];
            yield break;
        }

        if (targetGO != null)
            yield return targetGO;
    }

    // 모든 아군
    public List<GameObject> GetAllies()
    {
        // 태그 기준: PlayerUnit이면 RunManager.playerUnits, EnemyUnit이면 RunManager.enemyUnits
        if (casterFsm != null && casterFsm.CompareTag("EnemyUnit"))
            return run.enemyUnits;
        return run.playerUnits;
    }

    // 모든 적
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
            if (!includeSelf && caster != null && go == caster.gameObject) continue;
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
