using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/SkillEffects/Teleport Adjacent To Backline Enemy")]
public class SE_TeleportAdjacentToBacklineEnemy : SkillEffectDefinition
{
    [Header("우선순위: 후방(침투) 타일 먼저 시도")]
    public bool preferBacklineSide = true;

    public override void Execute(SkillContext ctx)
    {
        /*if (ctx.casterFsm == null || ctx.tileMap == null || ctx.run == null)
        {
            Debug.LogWarning("[SE_TeleportAdjacentToBacklineEnemy] Missing context data");
            return;
        }*/

        if(ctx.casterFsm == null)
        {
            Debug.LogWarning("[SE_TeleportAdjacentToBacklineEnemy] Missing context data 캐스터 FSM");
            return;
        }

        if(ctx.run == null)
        {
            Debug.LogWarning("[SE_TeleportAdjacentToBacklineEnemy] Missing context data 런매니저");
            return;
        }

        if(ctx.tileMap == null)
        {
            Debug.LogWarning("[SE_TeleportAdjacentToBacklineEnemy] Missing context data 타일맵");
            return;
        }
        

        // 적 목록 가져오기
        List<GameObject> enemies = ctx.casterFsm.CompareTag("EnemyUnit")
            ? ctx.run.playerUnits
            : ctx.run.enemyUnits;

        if (enemies == null || enemies.Count == 0) return;

        // 후방 타겟 선정: Player면 최대 x, Enemy면 최소 x
        bool casterIsPlayer = ctx.casterFsm.CompareTag("PlayerUnit");

        UnitFSM target = null;
        int bestX = casterIsPlayer ? int.MinValue : int.MaxValue;

        foreach (var go in enemies)
        {
            if (go == null || !go.activeInHierarchy) continue;

            var u = go.GetComponent<Unit>();
            if (u == null || u.hp <= 0) continue;

            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            int x = fsm.currentTilePosition.x;

            if (casterIsPlayer)
            {
                if (x > bestX) { bestX = x; target = fsm; }
            }
            else
            {
                if (x < bestX) { bestX = x; target = fsm; }
            }
        }

        if (target == null) return;

        // 3) 타겟 주변 인접칸 중 "침투 방향" 우선으로 자리 고르기
        //    Player는 +x가 더 후방(적 진영 더 깊숙이),
        //    Enemy는 -x가 더 후방(플레이어 진영 더 깊숙이)
        int invadeDirX = casterIsPlayer ? +1 : -1;

        Vector2Int t = target.currentTilePosition;

        // 후보 우선순위
        List<Vector2Int> candidates = new List<Vector2Int>();

        Vector2Int backlineSide = new Vector2Int(t.x + invadeDirX, t.y);
        Vector2Int frontlineSide = new Vector2Int(t.x - invadeDirX, t.y);

        if (preferBacklineSide)
        {
            candidates.Add(backlineSide);       // 제일 먼저: 더 깊숙한 칸
            candidates.Add(t + Vector2Int.up);
            candidates.Add(t + Vector2Int.down);
            candidates.Add(frontlineSide);
        }
        else
        {
            candidates.Add(t + Vector2Int.up);
            candidates.Add(t + Vector2Int.down);
            candidates.Add(backlineSide);
            candidates.Add(frontlineSide);
        }

        // 4) 점유 체크(타일데이터 Status == -1이 점유)
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        foreach (var td in ctx.tileMap.tileDataList)
        {
            if (td.Status == -1)
                occupied.Add(td.Position);
        }

        // 내 현재 칸은 비워야하니까 occupied에서 제외
        occupied.Remove(ctx.casterFsm.currentTilePosition);

        Vector2Int chosen = ctx.casterFsm.currentTilePosition;
        bool found = false;

        foreach (var c in candidates)
        {
            if (!ctx.tileMap.IsWalkable(c)) continue;
            if (occupied.Contains(c)) continue;

            chosen = c;
            found = true;
            break;
        }

        if (!found) return;

        // 텔포 실행: 기존 타일 비우고, 새 위치로 순간이동
        ctx.tileMap.SetTileStatus(ctx.casterFsm.currentTilePosition, 0);
        ctx.casterFsm.SetPositionInstant(chosen); // 내부에서 새 타일을 -1로 마킹함 :contentReference[oaicite:2]{index=2}

        // 타겟도 후방 타겟으로 바꿔주기
        ctx.casterFsm.targetEnemy = target.gameObject;
    }
}
