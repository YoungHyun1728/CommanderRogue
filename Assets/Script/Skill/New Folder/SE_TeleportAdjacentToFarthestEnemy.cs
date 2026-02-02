using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/SkillEffects/Teleport Adjacent To Farthest Enemy")]
public class SE_TeleportAdjacentToFarthestEnemy : SkillEffectDefinition
{
    public bool preferBehind = true; // 선택 옵션(지금은 단순화)

    public override void Execute(SkillContext ctx)
    {
        if (ctx.casterFsm == null) return;

        var enemies = ctx.GetEnemies();
        UnitFSM farFsm = null;
        int bestDist = -1;

        foreach (var go in enemies)
        {
            if (go == null || !go.activeInHierarchy) continue;
            var u = go.GetComponent<Unit>();
            if (u == null || u.hp <= 0) continue;

            var efsm = go.GetComponent<UnitFSM>();
            if (efsm == null) continue;

            int d = Mathf.Abs(efsm.currentTilePosition.x - ctx.casterFsm.currentTilePosition.x)
                  + Mathf.Abs(efsm.currentTilePosition.y - ctx.casterFsm.currentTilePosition.y);

            if (d > bestDist)
            {
                bestDist = d;
                farFsm = efsm;
            }
        }

        if (farFsm == null) return;

        // 적 인접 4방향 후보
        var targetTile = farFsm.currentTilePosition;
        var candidates = new List<Vector2Int>
        {
            targetTile + Vector2Int.left,
            targetTile + Vector2Int.right,
            targetTile + Vector2Int.up,
            targetTile + Vector2Int.down
        };

        // 점유 체크(타일맵 내부 상태를 몰라도, 유닛들의 타일 포지션으로 안전하게 검사)
        bool IsOccupied(Vector2Int t)
        {
            var all = new List<GameObject>();
            all.AddRange(ctx.run.playerUnits);
            all.AddRange(ctx.run.enemyUnits);

            foreach (var go in all)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var ufsm = go.GetComponent<UnitFSM>();
                if (ufsm == null) continue;
                if (ufsm.currentTilePosition == t) return true;
            }
            return false;
        }

        Vector2Int chosen = ctx.casterFsm.currentTilePosition;
        bool found = false;

        foreach (var c in candidates)
        {
            if (ctx.tileMap != null && !ctx.tileMap.IsWalkable(c)) continue;
            if (IsOccupied(c)) continue;

            chosen = c;
            found = true;
            break;
        }

        if (!found) return;

        // 기존 타일 비우고(죽을 때도 0으로 비움) 새 타일 점유 :contentReference[oaicite:7]{index=7}
        if (ctx.tileMap != null)
            ctx.tileMap.SetTileStatus(ctx.casterFsm.currentTilePosition, 0);

        ctx.casterFsm.SetPositionInstant(chosen);
    }
}
