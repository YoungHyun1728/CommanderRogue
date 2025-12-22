using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UnitFSM))]
[DefaultExecutionOrder(-3)]
public class UnitGridAgent : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TileMapManager tileMapManager;
    [SerializeField] private BattleMovementSystem movementSystem;

    private UnitFSM fsm;
    private Unit unit;
    private Rigidbody2D rb;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 4f;

    public Vector2Int TilePos { get; private set; }
    public bool IsInterpolating { get; private set; }

    private GameObject targetEnemy;

    // path cache
    private List<Vector2Int> cachedPath;
    private float repathCooldown;
    private int failCount;

    // melee slot stickiness
    private bool hasPreferredMeleeTile;
    private Vector2Int preferredMeleeTile;
    private int preferredFailCount;
    private int preferredTargetId;

    // repath stabilization
    private Vector2Int lastGoalTile;
    private Vector2Int lastEnemyTile;

    // diagonal (dx=1,dy=1) melee-vs-melee "yield once"
    private bool diagonalYieldedOnce;
    private int diagonalYieldTargetId;

    public void Initialize(TileMapManager tm, BattleMovementSystem ms, int unitId, Vector2Int startTile, Unit u)
    {
        tileMapManager = tm;
        movementSystem = ms;
        TilePos = startTile;
        unit = u;

        if (fsm != null)
        {
            fsm.unitId = unitId;
            fsm.currentTilePosition = startTile;
        }

        // 초기 상태 리셋
        cachedPath = null;
        repathCooldown = 0f;
        failCount = 0;

        hasPreferredMeleeTile = false;
        preferredFailCount = 0;

        diagonalYieldedOnce = false;
        diagonalYieldTargetId = 0;

        if (movementSystem != null)
            movementSystem.Register(this);
    }

    private void Awake()
    {
        fsm = GetComponent<UnitFSM>();
        unit = GetComponent<Unit>();
        rb = GetComponent<Rigidbody2D>();

        if (movementSystem == null)
            movementSystem = FindAnyObjectByType<BattleMovementSystem>();

        if (tileMapManager == null)
            tileMapManager = FindAnyObjectByType<TileMapManager>();
    }

    private void OnEnable()
    {
        if (movementSystem != null)
            movementSystem.Register(this);
    }

    private void OnDisable()
    {
        if (movementSystem != null)
            movementSystem.Unregister(this);
    }

    public void SetTarget(GameObject enemy)
    {
        targetEnemy = enemy;

        int id = enemy != null ? enemy.GetInstanceID() : 0;

        // 타겟 바뀌면 슬롯/대각선 yield 상태 리셋
        if (id != preferredTargetId)
        {
            preferredTargetId = id;
            hasPreferredMeleeTile = false;
            preferredFailCount = 0;

            diagonalYieldedOnce = false;
            diagonalYieldTargetId = id;

            cachedPath = null;
            failCount = 0;
            repathCooldown = 0f;
        }
    }

    public bool TryBuildIntent(out BattleMovementSystem.MoveIntent intent)
    {
        intent = default;

        if (tileMapManager == null || movementSystem == null) return false;
        if (fsm == null || unit == null) return false;

        // Move 상태에서만
        if (fsm.CurrentState != UnitFSM.UnitState.Move) return false;

        // 타일 사이면 패스
        if (IsInterpolating) return false;

        if (targetEnemy == null) return false;

        Unit enemyUnit = targetEnemy.GetComponent<Unit>();
        if (enemyUnit == null) return false;

        Vector2Int enemyTileNow = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);

        // ✅ 대각선 문제(근접 vs 근접, dx=1 dy=1): 한 번 반려
        if (unit.attackRange == 1 && enemyUnit.attackRange == 1)
        {
            int dx = Mathf.Abs(enemyTileNow.x - TilePos.x);
            int dy = Mathf.Abs(enemyTileNow.y - TilePos.y);

            bool diagonal = (dx == 1 && dy == 1);

            if (!diagonal)
            {
                // 대각선 상황이 풀리면 다음에 다시 "한 번 반려" 가능하도록 리셋
                diagonalYieldedOnce = false;
            }
            else
            {
                // 아직 한 번도 반려 안 했고, 내가 반려 대상이면 이번 틱은 의도 제출 X
                int tid = targetEnemy.GetInstanceID();
                if (diagonalYieldTargetId != tid)
                {
                    diagonalYieldTargetId = tid;
                    diagonalYieldedOnce = false;
                }

                if (!diagonalYieldedOnce && ShouldYieldDiagonal(enemyUnit))
                {
                    diagonalYieldedOnce = true;
                    return false; // ✅ 이번 틱 "이동 반려"
                }
            }
        }

        Vector2Int goalTile = ChooseGoalTile(enemyTileNow);
        if (goalTile == TilePos) return false;

        // ✅ 리패스 조건을 "덜 자주" (흔들림 감소)
        bool goalChanged = goalTile != lastGoalTile;
        bool enemyMoved = enemyTileNow != lastEnemyTile;

        repathCooldown -= Time.deltaTime;

        if (cachedPath == null || cachedPath.Count < 2 ||
            goalChanged || enemyMoved || failCount >= 1 || repathCooldown <= 0f)
        {
            cachedPath = BuildPath(TilePos, goalTile);
            repathCooldown = 0.40f; // 0.2 -> 0.4 (흔들림 줄이기)
            failCount = 0;

            lastGoalTile = goalTile;
            lastEnemyTile = enemyTileNow;
        }

        if (cachedPath == null || cachedPath.Count < 2) return false;

        Vector2Int next = cachedPath[1];

        // 다음 칸이 점유면 이번 틱은 포기(대기 while 금지)
        if (tileMapManager.GetTileStatus(next) == -1)
        {
            failCount++;
            if (unit.attackRange <= 1 && hasPreferredMeleeTile) preferredFailCount++;
            return false;
        }

        intent = new BattleMovementSystem.MoveIntent
        {
            unitId = fsm.unitId,
            priority = unit.totalAgility,  // ✅ double이어도 OK
            from = TilePos,
            to = next,
            agent = this
        };
        return true;
    }

    private bool ShouldYieldDiagonal(Unit enemyUnit)
    {
        double myAgi = unit.totalAgility;
        double enemyAgi = enemyUnit.totalAgility;

        // 느린 쪽이 반려
        if (myAgi < enemyAgi) return true;

        // 같으면 플레이어가 반려
        if (myAgi == enemyAgi && CompareTag("PlayerUnit"))
            return true;

        return false;
    }

    public void CommitMove(Vector2Int destTile)
    {
        TilePos = destTile;
        if (fsm != null) fsm.currentTilePosition = destTile;

        Vector3 targetWorld = tileMapManager.tilemap.GetCellCenterWorld(new Vector3Int(destTile.x, destTile.y, 0));
        StopCoroutine(nameof(MoveOneTile));
        StartCoroutine(MoveOneTile(targetWorld));
    }

    private IEnumerator MoveOneTile(Vector3 targetWorld)
    {
        IsInterpolating = true;

        while ((transform.position - targetWorld).sqrMagnitude > 0.0001f)
        {
            if (fsm.CurrentState != UnitFSM.UnitState.Move)
                break;

            Vector2 next = Vector2.MoveTowards(rb.position, (Vector2)targetWorld, Time.deltaTime * moveSpeed);
            rb.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetWorld);
        transform.position = targetWorld;
        IsInterpolating = false;
    }

    private Vector2Int ChooseGoalTile(Vector2Int enemyTile)
    {
        if (unit.attackRange <= 1)
        {
            // 1) 슬롯 유지(Stickiness)
            if (hasPreferredMeleeTile)
            {
                if (tileMapManager.IsWalkable(preferredMeleeTile) &&
                    (tileMapManager.GetTileStatus(preferredMeleeTile) != -1 || preferredFailCount < 3))
                {
                    return preferredMeleeTile;
                }

                hasPreferredMeleeTile = false;
                preferredFailCount = 0;
            }

            // 2) 새 슬롯 선택 (타이브레이크 포함)
            if (TryPickReachableAdjacent(enemyTile, out var adj))
            {
                preferredMeleeTile = adj;
                hasPreferredMeleeTile = true;
                return adj;
            }

            return enemyTile;
        }

        // 원거리: 적 타일을 목표(사거리/라인전 정책은 나중에 확장 가능)
        return enemyTile;
    }

    private bool TryPickReachableAdjacent(Vector2Int enemyTile, out Vector2Int best)
    {
        best = TilePos;

        var candidates = new List<Vector2Int>
        {
            enemyTile + Vector2Int.left,
            enemyTile + Vector2Int.right,
            enemyTile + Vector2Int.up,
            enemyTile + Vector2Int.down
        };

         int bestLen = int.MaxValue;
        int bestDxAfter = int.MaxValue;     // ✅ 후보 타일에 섰을 때 x축 거리
        int bestLaneDelta = int.MaxValue;   // ✅ 내 y 변화 최소
        int bestEnemyLane = int.MaxValue;

        var occupied = BuildOccupiedSet();

        foreach (var c in candidates)
        {
            if (!tileMapManager.IsWalkable(c)) continue;
            if (tileMapManager.GetTileStatus(c) == -1) continue;

            var p = AStarPathfinder.FindPath(TilePos, c, tileMapManager, occupied);
            if (p == null || p.Count < 2) continue;

            int len = p.Count;

            // 후보에 섰을 때 적과의 x 거리(작을수록 “정면으로 붙는 느낌”)
            int dxAfter = Mathf.Abs(enemyTile.x - c.x);

            // 내 y에서 덜 벗어나는 쪽(위아래 흔들림 방지)
            int laneDelta = Mathf.Abs(c.y - TilePos.y);

            // 적 y와 가까운 쪽(타이브레이크)
            int enemyLane = Mathf.Abs(c.y - enemyTile.y);

            // ✅ 우선순위: (1) 경로 길이 (2) x 거리 (3) y 변화 (4) 적 y
            if (len < bestLen ||
                (len == bestLen && dxAfter < bestDxAfter) ||
                (len == bestLen && dxAfter == bestDxAfter && laneDelta < bestLaneDelta) ||
                (len == bestLen && dxAfter == bestDxAfter && laneDelta == bestLaneDelta && enemyLane < bestEnemyLane))
            {
                bestLen = len;
                bestDxAfter = dxAfter;
                bestLaneDelta = laneDelta;
                bestEnemyLane = enemyLane;
                best = c;
            }
        }

        return bestLen != int.MaxValue;
    }

    private List<Vector2Int> BuildPath(Vector2Int start, Vector2Int goal)
    {
        var occupied = BuildOccupiedSet();
        return AStarPathfinder.FindPath(start, goal, tileMapManager, occupied);
    }

    private HashSet<Vector2Int> BuildOccupiedSet()
    {
        var occ = new HashSet<Vector2Int>();
        foreach (var td in tileMapManager.tileDataList)
        {
            if (td.Status == -1 && td.Position != TilePos)
                occ.Add(td.Position);
        }
        return occ;
    }

    public void ForceSyncToTile(Vector2Int tile)
    {
        StopAllCoroutines();
        IsInterpolating = false;

        TilePos = tile;

        if (fsm != null)
            fsm.currentTilePosition = tile;

        cachedPath = null;
        repathCooldown = 0f;
        failCount = 0;

        hasPreferredMeleeTile = false;
        preferredFailCount = 0;

        diagonalYieldedOnce = false;
        diagonalYieldTargetId = 0;
    }
}
