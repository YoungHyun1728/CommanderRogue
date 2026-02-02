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

    public GameObject targetGO;
    public Unit targetUnit;
    public UnitFSM targetFsm;

    public SkillContext(Unit caster, UnitFSM casterFsm, UnitSkillSystem skills, UnitStatusEffectController status, GameObject targetGO)
    {
        this.caster = caster;
        this.casterFsm = casterFsm;
        this.casterSkills = skills;
        this.casterStatus = status;

        this.tileMap = casterFsm != null ? casterFsm.GetComponentInParent<TileMapManager>() : Object.FindObjectOfType<TileMapManager>();
        this.run = RunManager.Instance;

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
}