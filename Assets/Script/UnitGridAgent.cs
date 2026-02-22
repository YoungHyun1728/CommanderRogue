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
    [SerializeField] private float moveSpeed;

    public Vector2Int TilePos { get; private set; }
    [SerializeField] private bool _isInterpolating; // 인스펙터용

    public bool IsInterpolating 
    {
        get { return _isInterpolating; }
        private set { _isInterpolating = value; }
    }

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
    private Vector2Int lastMoveFrom;
    private Vector2Int lastMoveTo;

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

        moveSpeed = unit.moveSpeed * 1.5f; // 타일 이동 속도 보정
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
        if (IsInterpolating)
        {
            Debug.Log("타일사이입니다.;");
            return false;
        } 

        if (targetEnemy == null) return false;

        Unit enemyUnit = targetEnemy.GetComponent<Unit>();
        if (enemyUnit == null) return false;

        Vector2Int enemyTileNow;

        var enemyAgent = targetEnemy.GetComponent<UnitGridAgent>();
        if (enemyAgent != null)
        {
            enemyTileNow = enemyAgent.TilePos;
        }
        else
        {
            enemyTileNow = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);
        }

        // 이미 공격 가능하면 이동 의도 내지 말고 공격 상태로 전환 시도
        if (fsm.CheckAttackRange())
        {
            fsm.TryChangeState(UnitFSM.UnitState.Attack);
            return false;
        }

        // 대각선 문제(근접 vs 근접, dx=1 dy=1): 한 번 반려
        if (unit.attackRange == 1 && enemyUnit.attackRange == 1)
        {
            int dx = Mathf.Abs(enemyTileNow.x - TilePos.x);
            int dy = Mathf.Abs(enemyTileNow.y - TilePos.y);

            bool diagonal = (dx == 1 && dy == 1);

            if (!diagonal)
            {
                // 대각선 상황이 풀리면 다음에 다시 "한 번 반려"
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
                    return false; // 이번 틱 "이동 반려"
                }
            }
        }

        Vector2Int goalTile = ChooseGoalTile(enemyTileNow);

        // goal이 현재 타일이면 이동 의도 제출 X
        if (goalTile == TilePos) return false;

        // 리패스 조건을 "덜 자주" (흔들림 감소)
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

        if (cachedPath == null || cachedPath.Count < 2)
        {
            Debug.Log($"[IntentFail] {name} pathNullOrShort goal={goalTile}");
            return false;
        }

        // ============================================================
        // (Fix #1) 원거리끼리 Y 맞추다 X 안 줄어드는 문제 방지:
        // 사거리 밖이면 "첫 칸"은 X를 줄이는 방향을 우선 시도한다.
        // ============================================================
        if (unit.attackRange > 1)
        {
            int dxToEnemy = enemyTileNow.x - TilePos.x;
            int dyToEnemy = enemyTileNow.y - TilePos.y;
            int manhattan = Mathf.Abs(dxToEnemy) + Mathf.Abs(dyToEnemy);

            if (manhattan > unit.attackRange)
            {
                // X가 0이 아니면 X를 먼저 줄이는 한 칸 시도
                if (dxToEnemy != 0)
                {
                    Vector2Int stepX = new Vector2Int(TilePos.x + (dxToEnemy > 0 ? 1 : -1), TilePos.y);

                    // 이동 가능 + 비어있으면 즉시 이 칸을 intent로 제출
                    if (tileMapManager.IsWalkable(stepX) && tileMapManager.GetTileStatus(stepX) != -1)
                    {
                        intent = new BattleMovementSystem.MoveIntent
                        {
                            unitId = fsm.unitId,
                            name = unit.unitName,
                            priority = unit.totalAgility,
                            from = TilePos,
                            to = stepX,
                            agent = this
                        };
                        return true;
                    }
                }
            }
        }

        Vector2Int next = cachedPath[1];

        // 다음 칸이 점유면 이번 틱은 포기(대기 while 금지)
        if (tileMapManager.GetTileStatus(next) == -1)
        {
            Debug.Log($"[IntentFail] {name} nextBlocked next={next}");
            failCount++;
            repathCooldown = 0f; //다음 틱에 바로 경로 재계산하도록
            if (unit.attackRange <= 1 && hasPreferredMeleeTile) preferredFailCount++;
            return false;
        }

        intent = new BattleMovementSystem.MoveIntent
        {
            unitId = fsm.unitId,
            name = unit.unitName,
            priority = unit.totalAgility,
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
        lastMoveFrom = TilePos;
        lastMoveTo = destTile;

        TilePos = destTile;
        if (fsm != null) fsm.currentTilePosition = destTile;

        Vector3 targetWorld = tileMapManager.tilemap.GetCellCenterWorld(new Vector3Int(destTile.x, destTile.y, 0));

        // 기존: StopCoroutine(nameof(MoveOneTile))  (거의 안 멈춤)
        StopAllCoroutines();          // <- 확실하게 한 개만 돌게
        StartCoroutine(MoveOneTile((Vector2)targetWorld));
    }

    private IEnumerator MoveOneTile(Vector2 targetWorld)
    {
        IsInterpolating = true;

        int dx = lastMoveTo.x - lastMoveFrom.x;
        moveSpeed = unit.moveSpeed * unit.moveSpeedMultiplier * 1.5f;

        if (dx < 0) fsm.FlipLeft();
        else if (dx > 0) fsm.FlipRight();

        // ✅ 시작 동기화(가끔 rb.position이 transform이랑 어긋나 있음)
        rb.position = transform.position;

        const float eps = 0.0001f;

        while ((rb.position - targetWorld).sqrMagnitude > eps)
        {
            if (fsm.CurrentState != UnitFSM.UnitState.Move)
                break;

            Vector2 next = Vector2.MoveTowards(rb.position, targetWorld, Time.fixedDeltaTime * moveSpeed);
            rb.MovePosition(next);

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetWorld);
        transform.position = targetWorld;

        IsInterpolating = false;

        if (fsm.CurrentState == UnitFSM.UnitState.Move && fsm.CheckAttackRange())
            fsm.TryChangeState(UnitFSM.UnitState.Attack);
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

            // ============================================================
            // (Fix #2 - 안전판) 인접 슬롯을 못 잡았을 때
            // enemyTile(점유 타일)로 goal 주면 "지나침/침투/스왑"이 생길 수 있으니 금지.
            // 대신 "적에게 가까워지는 1칸"을 목표로 잡아서 멈춤도 방지.
            // ============================================================
            return PickApproachTileTowardEnemy(enemyTile);
        }

        // 원거리: 기존대로 적 타일을 목표로 하되,
        // TryBuildIntent에서 "X 우선 1칸"을 먼저 시도해서 Y 흔들림을 억제한다.
        return PickRangedGoalTile(enemyTile, unit.attackRange);
    }

    private Vector2Int PickRangedGoalTile(Vector2Int enemyTile, int range)
    {
        Vector2Int best = TilePos;
        int bestLen = int.MaxValue;

        // 현재 타일이 이미 사거리 안이면 굳이 적 옆으로 갈 필요 없음
        int curDist = Mathf.Abs(enemyTile.x - TilePos.x) + Mathf.Abs(enemyTile.y - TilePos.y);
        if (curDist <= range) return TilePos;

        var occupied = BuildOccupiedSet();

        // 적을 중심으로 "마름모 범위(range)" 안의 후보를 찾는다
        for (int dx = -range; dx <= range; dx++)
        {
            int rem = range - Mathf.Abs(dx);
            for (int dy = -rem; dy <= rem; dy++)
            {
                Vector2Int c = new Vector2Int(enemyTile.x + dx, enemyTile.y + dy);

                if (!tileMapManager.IsWalkable(c)) continue;
                if (tileMapManager.GetTileStatus(c) == -1) continue; // 점유면 제외

                // 후보까지의 경로 길이로 가장 좋은 자리 선택
                var p = AStarPathfinder.FindPathInternal(TilePos, c, tileMapManager, occupied);
                if (p == null || p.Count < 2) continue;

                int len = p.Count;
                if (len < bestLen)
                {
                    bestLen = len;
                    best = c;
                }
            }
        }

        // 사거리 안 후보를 못 찾으면, 기존 안전판(적에게 가까워지는 1칸)으로
        if (bestLen == int.MaxValue)
            return PickApproachTileTowardEnemy(enemyTile);

        return best;
    }

    private Vector2Int PickApproachTileTowardEnemy(Vector2Int enemyTile)
    {
        var dirs = new Vector2Int[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

        int bestDist = Mathf.Abs(enemyTile.x - TilePos.x) + Mathf.Abs(enemyTile.y - TilePos.y);
        Vector2Int best = TilePos;

        foreach (var d in dirs)
        {
            var n = TilePos + d;
            if (!tileMapManager.IsWalkable(n)) continue;
            if (tileMapManager.GetTileStatus(n) == -1) continue;

            int nd = Mathf.Abs(enemyTile.x - n.x) + Mathf.Abs(enemyTile.y - n.y);
            if (nd < bestDist)
            {
                bestDist = nd;
                best = n;
            }
        }

        // 줄어드는 칸이 없으면 대기
        return best;
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
        int bestDxAfter = int.MaxValue;     // 후보 타일에 섰을 때 x축 거리
        int bestLaneDelta = int.MaxValue;   // 내 y 변화 최소
        int bestEnemyLane = int.MaxValue;

        var occupied = BuildOccupiedSet();

        foreach (var c in candidates)
        {
            if (!tileMapManager.IsWalkable(c)) continue;
            if (tileMapManager.GetTileStatus(c) == -1) continue;

            var p = AStarPathfinder.FindPath(TilePos, c, tileMapManager, occupied);
            if (p == null || p.Count < 2) continue;

            int len = p.Count;

            // 후보에 섰을 때 적과의 x 거리
            int dxAfter = Mathf.Abs(enemyTile.x - c.x);

            // 내 y에서 덜 벗어나는 쪽
            int laneDelta = Mathf.Abs(c.y - TilePos.y);

            // 적 y와 가까운 쪽
            int enemyLane = Mathf.Abs(c.y - enemyTile.y);

            // 우선순위: (1) 경로 길이 (2) x 거리 (3) y 변화 (4) 적 y
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

    public void NotifyMoveRejected()
    {
        // 다음 틱에 경로 재계산/슬롯 재선정 유도
        failCount++;

        if (unit != null && unit.attackRange <= 1 && hasPreferredMeleeTile)
            preferredFailCount++;

        repathCooldown = 0f;
    }
}
