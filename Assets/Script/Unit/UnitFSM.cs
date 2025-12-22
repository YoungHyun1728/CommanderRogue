using System.Collections;
using System.Collections.Generic;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UnitFSM : MonoBehaviour
{
    public enum UnitState{ Ready, Idle, Move, Attack, Faint } // 유닛 상태 정의

    [SerializeField] private UnitState currentState; // 현재 상태
    public UnitState CurrentState => currentState; // 이동을 위한 읽기용
    [SerializeField] private RectTransform hudRoot; // HUD바 회전
    [SerializeField] private ProjectileType projectileType; // 투사체 타입
    [SerializeField] private Transform projectileSpawnPoint; // 투사체 생성 위치
    [SerializeField] private UnitGridAgent gridAgent;

    public int unitId; // 유닛 고유 ID
    
    [Header("유닛 기본 속성")]
    Unit unit;

    // 유닛의 타일맵 관리자 참조
    private TileMapManager tileMapManager;
    // 현재 타일맵에서의 위치
    public Vector2Int currentTilePosition;
    private Vector2Int moveTargetTile;
    private Vector2Int lastTilePosition;
    public GameObject targetEnemy;
    private RectTransform rect;
    private Animator animator;
    Rigidbody2D rb;

    public bool isMoving = false;

    public void Initialize(TileMapManager tileMapManager, Vector2Int initialPosition)
    {
        // 타일맵 관리자 참조 저장
        this.tileMapManager = tileMapManager;
        if (gridAgent == null) gridAgent = GetComponent<UnitGridAgent>();
        SetPositionInstant(initialPosition);
        BattleMovementSystem ms = FindAnyObjectByType<BattleMovementSystem>();
        gridAgent.Initialize(tileMapManager, ms, unitId, currentTilePosition, unit);        
    }

    public void SetPositionInstant(Vector2Int tilePosition)
    {
        // 유닛 초기 위치 설정
        currentTilePosition = tilePosition;

        // 유닛의 월드 좌표 동기화
        Vector3Int cell = new Vector3Int(tilePosition.x, tilePosition.y, 0);
        Vector3 tileCenter = tileMapManager.tilemap.GetCellCenterWorld(cell);

        // 유닛 위치 설정
        transform.position = tileCenter;

        if (gridAgent == null) gridAgent = GetComponent<UnitGridAgent>();
        if (gridAgent != null) gridAgent.ForceSyncToTile(tilePosition);

        Debug.Log($"[Unit] 초기 위치 설정: {transform.position} (중심: {tileCenter})");

        // 새 위치 타일 상태 업데이트
        tileMapManager.UpdateTileStatus(currentTilePosition);
    }

    void Awake()
    {
        unit = GetComponent<Unit>();
        unitId = GetInstanceID(); // 고유 ID를 Unity의 InstanceID로 설정
        currentState = UnitState.Ready; // 기본상태를 Ready
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        rect = GetComponent<RectTransform>(); //회전에 이용할 컴포넌트초기화
    }
    
    void Start()
    {
        tileMapManager = FindObjectOfType<TileMapManager>();        
    }
    
    void Update()
    {
        //타일맵 매니저에게 자신의 위치를 계속 알려줌
        Vector2Int newTilePosition = GetTileFromWorldPosition();

        switch (currentState)
        {
            case UnitState.Idle:
                HandleIdleState();
                break;
            case UnitState.Move:
                HandleMoveState();
                break;
            case UnitState.Attack:
                HandleAttackState();
                break;
            case UnitState.Faint:
                break;
        }

        //전투 중 hp회복
        if(currentState == UnitState.Idle || currentState == UnitState.Attack || currentState == UnitState.Move)
        {
            if(unit.hp < unit.maxHp && unit.hp > 0)
            {
                unit.HpRegen(Time.deltaTime);
            }
        }
    }

    private void ChangeState(UnitState newState)
    {
        if (currentState == newState) return;

        Debug.Log($"[Unit] State Changed: {currentState} -> {newState}");
        
        // 실행 중인 모든 코루틴 종료
        StopAllCoroutines();
        
        currentState = newState;

        // 상태 변경 후 초기화 작업
        switch (newState)
        {
            case UnitState.Idle:
                OnEnterIdle();
                break;
            case UnitState.Move:
                OnEnterMove();
                break;
            case UnitState.Attack:
                OnEnterAttack();
                break;
            case UnitState.Faint:
                OnEnterFaint();
                break;
        }
    }

    // 상태 진입 시 초기화
    private void OnEnterIdle()
    {
        //animator.speed = 1f; // 애니메이션 속도 1로 복귀
        animator.SetFloat("Speed", 0f);
        isMoving = false;// 이동 중지 플래그 초기화
    }
    private void OnEnterMove()
    {
        //animator.speed = 1f; // 애니메이션 속도 1로 복귀
        //타겟없을때 State변경
        if (targetEnemy == null)
        {
            ChangeState(UnitState.Idle);
            return;
        }

        animator.SetFloat("Speed", 1f); //이동 애니메이션 시작
        
        Vector2Int enemyTile = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);

        if (unit.attackRange == 1)
        {
            // 도달 가능한 인접칸만 목표로 잡기
            if (!TryGetBestAttackTileReachable(enemyTile, out moveTargetTile))
            {
                // 붙을 자리가 없으면(완전 포위 등) 일단 Idle로 보내서 타겟 재선정/상황변화를 기다림
                ChangeState(UnitState.Idle);
                return;
            }
        }
        else
        {
            moveTargetTile = enemyTile;
        }
    }
    private void OnEnterAttack()
    {
        isMoving = false; // 이동 중지 플래그 초기화
        animator.SetFloat("Speed", 0f); //이동 애니메이션 종료
        if (targetEnemy == null)
        {
            ChangeState(UnitState.Idle);
            return;
        }

        StartCoroutine(AttackCorotione());
    }
    private void OnEnterFaint()
    {
        isMoving = false; // 이동 중지 플래그 초기화
        // 기절 애니메이션 추가

        OnDeath(); // 인보크로 1초 후 삭제
        
        //animator.speed = 1f; // 애니메이션 속도 1로 복귀
        // 기절시 데이터 저장 후 오브젝트 삭제?
        // 적은 삭제 해야하는데 플레이어쪽은 삭제하면 안되는데
    }

    private void HandleIdleState()
    {
        // 타겟이 없거나 기절한 경우 가장 가까운 적 찾기
        if(targetEnemy == null || targetEnemy.GetComponent<Unit>().hp <= 0)
        {
            targetEnemy = FindClosestEnemy();
        }

        if(targetEnemy != null)
        {   
            Debug.Log($"{targetEnemy.name} HandleIdleState CheckAttackRange");
            if (CheckAttackRange())
            {
                Debug.Log($"[Unit] 타겟이 공격 범위 내에 있음, Attack 상태로 전환");                
                ChangeState(UnitState.Attack);
            }
            else
            {
                Debug.Log("[Unit] 타겟 발견, Move 상태로 전환");
                ChangeState(UnitState.Move); // 공격 범위에 없으면 Move 상태로 전환
            }             
        }
        else
        {
            Debug.Log("[Unit] 타겟 없음, 대기 중");
        }
    }

    // MOVE 상태
    private void HandleMoveState()
    {
        // 타겟이 없거나 기절한 경우 Idle 상태로 전환
        if (targetEnemy == null || targetEnemy.GetComponent<Unit>().hp <= 0)
        {
            // 타겟이 없거나 사망한 경우 Idle 상태로 전환
            ChangeState(UnitState.Idle);
            return;
        }
        
        if (CheckAttackRange())
        {
            ChangeState(UnitState.Attack);
            return;
        }
        
        if (gridAgent != null)
            gridAgent.SetTarget(targetEnemy);
    }

    private void HandleAttackState()
    {
        
    }

    // FAINT 상태
    private void HandleFaintState()
    {
        Debug.Log($"[Unit] 기절. No further actions.");
        
        if(unit.hp > 1)
        {
            Debug.Log($"[Unit] 부활!!!");
            ChangeState(UnitState.Idle);
            return;
        }
    }

    private Vector2Int GetTileFromWorldPosition()
    {
        Vector3Int cellPosition = tileMapManager.tilemap.WorldToCell(transform.position);
        return new Vector2Int(cellPosition.x, cellPosition.y);
    }

    // 근접이 "붙을 자리"를 고를 때: 4방향 후보 중
    // 1) Walkable(또는 현재 위치 허용)
    // 2) A* 경로가 존재하는 후보만
    // 3) 경로 길이가 가장 짧은 후보 선택
    private bool TryGetBestAttackTileReachable(Vector2Int enemyTilePosition, out Vector2Int bestTile)
    {
        // 근접이면 enemy 주변 4칸이 목표 후보
        List<Vector2Int> candidates = new List<Vector2Int>
        {
            enemyTilePosition + Vector2Int.left,
            enemyTilePosition + Vector2Int.right,
            enemyTilePosition + Vector2Int.up,
            enemyTilePosition + Vector2Int.down
        };

        // 점유 타일 set 만들기(네 기존 방식 유지)
        HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();
        foreach (var tileData in tileMapManager.tileDataList)
        {
            if (tileData.Status == -1 && tileData.Position != currentTilePosition)
                occupiedTiles.Add(tileData.Position);
        }

        int bestLen = int.MaxValue;
        bestTile = currentTilePosition;

        foreach (var cand in candidates)
        {
            // 내가 이미 그 칸에 서 있는 건 OK
            if (cand != currentTilePosition && !tileMapManager.IsWalkable(cand))
                continue;

            // cand까지 경로가 실제로 존재하는지 확인
            var path = AStarPathfinder.FindPath(currentTilePosition, cand, tileMapManager, occupiedTiles);
            if (path == null || path.Count < 2)
                continue;

            if (path.Count < bestLen)
            {
                bestLen = path.Count;
                bestTile = cand;
            }
        }

        return bestLen != int.MaxValue;
    }

    //캐릭터가 가야할 다음 타일을 보내는 용도
    private IEnumerator MoveToCoroutine(Vector2Int targetTile)
    {
        if (isMoving) yield break;
        isMoving = true;

        // 목표와 동일하면 종료
        if (currentTilePosition == targetTile)
        {
            isMoving = false;
            yield break;
        }
        
        // 서로 대각선일때 싸우지안는 문제 보정
        if (targetEnemy != null && unit.attackRange == 1 && targetEnemy.GetComponent<Unit>().attackRange == 1)
        {
            Vector2Int enemyTile = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);
            int diffX = Mathf.Abs(enemyTile.x - currentTilePosition.x);
            int diffY = Mathf.Abs(enemyTile.y - currentTilePosition.y);

            // 민첩수치로 대기자 결정
            if (diffX == 1 && diffY == 1)
            {
                if(unit.totalAgility < targetEnemy.GetComponent<Unit>().totalAgility)
                {                    
                    animator.SetFloat("Speed", 0f); //이동 애니메이션 종료
                    yield return new WaitForSeconds(0.49f);
                }
                else if (unit.totalAgility == targetEnemy.GetComponent<Unit>().totalAgility)
                {
                    if(CompareTag("PlayerUnit"))
                    {
                        animator.SetFloat("Speed", 0f); //이동 애니메이션 종료
                        yield return new WaitForSeconds(0.49f);                        
                    }
                }

                Debug.Log($"{this.gameObject.name}대각선 문제 대기 테스트!!!!!");
            }
        }

        // 경로 계산
        HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>(); 
        foreach (var tileData in tileMapManager.tileDataList)
        {
            // 이동불가 타일 (점유된타일, 타일이 없는곳)
            if (tileData.Status == -1 && tileData.Position != currentTilePosition)
            {
                occupiedTiles.Add(tileData.Position);
            }
        }

        List<Vector2Int> path = AStarPathfinder.FindPath(currentTilePosition, targetTile, tileMapManager, occupiedTiles);

        if (path == null || path.Count < 2)
        {
            Debug.LogWarning($"[Unit] 경로를 찾을 수 없습니다! 잠시 대기 후 재시도 - {gameObject.name}");
            yield return new WaitForSeconds(0.3f);
            isMoving = false; // 다음 프레임에 다시 시도하도록
            yield break;
        }
        // 첫 번째 타일로 이동
        Vector2Int nextStep = path[1];

        // nextSetp 보정 (거리가 줄지 않는 방향으로 반복이동 문제 수정)
        int dx = targetTile.x - currentTilePosition.x;
        int dy = targetTile.y - currentTilePosition.y;
        int currentDist = Mathf.Abs(dx) + Mathf.Abs(dy);

        // 우선순위: |dx| >= |dy|면 가로 먼저, 아니면 세로 먼저
        List<Vector2Int> dirPriority = new List<Vector2Int>();

        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
        {
            // 가로 → 세로
            if (dx != 0) dirPriority.Add(new Vector2Int(Mathf.Clamp(dx, -1, 1), 0));
            if (dy != 0) dirPriority.Add(new Vector2Int(0, Mathf.Clamp(dy, -1, 1)));
        }
        else
        {
            // 세로 → 가로
            if (dy != 0) dirPriority.Add(new Vector2Int(0, Mathf.Clamp(dy, -1, 1)));
            if (dx != 0) dirPriority.Add(new Vector2Int(Mathf.Clamp(dx, -1, 1), 0));
        }

        foreach (var dir in dirPriority)
        {
            Vector2Int candidate = currentTilePosition + dir;

            if (!tileMapManager.IsWalkable(candidate))
                continue;

            int newDist = Mathf.Abs(targetTile.x - candidate.x) + Mathf.Abs(targetTile.y - candidate.y);

            // 거리 줄어드는 방향만 허용
            if (newDist < currentDist)
            {
                nextStep = candidate;
                break;
            }
        }

        bool overridden = false;

        foreach (var dir in dirPriority)
        {
            Vector2Int candidate = currentTilePosition + dir;

            if (!tileMapManager.IsWalkable(candidate))
                continue;

            // 바로 이전에 있던 타일로는 안 가려고 한다
            if (candidate == lastTilePosition)
                continue;

            int newDist = Mathf.Abs(targetTile.x - candidate.x) + Mathf.Abs(targetTile.y - candidate.y);

            // 타겟과의 거리가 줄어드는 경우만 보정
            if (newDist < currentDist)
            {
                nextStep = candidate;
                overridden = true;
                break;
            }
        }

        if (nextStep == lastTilePosition && path.Count >= 3)
            nextStep = path[2];

        // 실제 이동
        Vector2Int prevTile = currentTilePosition;
        //Debug.Log($"[Unit] 다음 타일로 이동: {nextStep}");
        yield return StartCoroutine(FollowPath(nextStep));
        
        lastTilePosition = prevTile;
        isMoving = false;
    }    

    //다음 타일로 이동
    private IEnumerator FollowPath(Vector2Int targetTile)
    {
        Vector3 targetPosition = tileMapManager.tilemap.GetCellCenterWorld(
            new Vector3Int(targetTile.x, targetTile.y, 0)
        );

        // 방향 전환
        Vector3 rotation = rect.localEulerAngles;
        if (targetEnemy != null && targetTile.x <= currentTilePosition.x) rotation.y = 0;
        else rotation.y = 180;

        rect.localEulerAngles = rotation;
        if (hudRoot != null) hudRoot.localEulerAngles = rotation;

        animator.SetFloat("Speed", 1f);

        bool reserved = false;

        // ✅ 예약 1회 시도
        if (!tileMapManager.TryReserveTileForMove(targetTile, unitId))
        {
            animator.SetFloat("Speed", 0f);
            isMoving = false;
            yield break;
        }

        reserved = true;

        try
        {
            // 이동
            while ((transform.position - targetPosition).sqrMagnitude > 0.0001f)
            {
                if (currentState != UnitState.Move)
                    yield break;

                rb.MovePosition(Vector3.MoveTowards(rb.position, targetPosition, Time.deltaTime * 3f));
                yield return new WaitForFixedUpdate();
            }

            transform.position = targetPosition;

            // ✅ "도착" 처리(중요)
            currentTilePosition = targetTile;
            isMoving = false;

            animator.SetFloat("Speed", 0f);

            // ✅ 여기서 ChangeState를 바로 호출하면 (StopAllCoroutines이면) finally가 안 돌 수 있음
            // 그래서 여기서는 판단만 하고, 상태 변경은 밖에서 하는 걸 추천함.
            // 만약 지금 당장 유지하고 싶으면 "반드시 finally 전에 예약을 풀고" ChangeState 해야 함.
            
            // (권장) 밖에서 처리하도록 플래그만 남기기
            // 예: arrivedAtTileThisFrame = true;  같은 변수로
        }
        finally
        {
            // ✅ 예약 해제는 여기서 딱 1번만
            if (reserved)
                tileMapManager.ReleaseReservedTile(targetTile, unitId);
        }
    }

    private int GetReservationState()
    {
        return unitId; // 고유 예약 상태
    }

    //가장 가까운 적을 지정해주는 함수
    private GameObject FindClosestEnemy()
    {
        GameObject closestEnemy = null;
        float closestDistance = float.MaxValue;
        List<GameObject> targetList = null;

        if (this.CompareTag("PlayerUnit"))
        {
            targetList = tileMapManager.enemyUnits;
        }
        else if (this.CompareTag("EnemyUnit"))
        {
            targetList = tileMapManager.playerUnits;
        }
        else
        {
            Debug.Log("[Unit] 유닛 태그가 올바르지 않습니다. 'PlayerUnit' 또는 'EnemyUnit'이어야 합니다.");
            return null;
        }

        // 타겟 리스트가 비어있는 경우 null 반환
        if(targetList == null || targetList.Count == 0)
            return null;
        

        foreach (GameObject enemy in targetList)
        {
            if (enemy == null) continue;
            if (enemy.GetComponent<Unit>().hp <= 0) continue; // 죽은 적은 무시

            Vector2Int enemyTilePosition = tileMapManager.GetTileFromWorldPosition(enemy.transform.position);
            float distance = Vector2Int.Distance(currentTilePosition, enemyTilePosition);

            if (distance < closestDistance)
            {
                closestEnemy = enemy;
                closestDistance = distance;
            }
        }
        
        return closestEnemy;
    }

    // 초기값 : 1초마다 업데이트 되게 실행
    IEnumerator UpdateTargetEnemy(float interval) 
    {
        while (true)
        {
            targetEnemy = FindClosestEnemy();
            yield return new WaitForSeconds(interval);
        }
    }

    // 공격 범위 타일 계산 인접타일을 range크기만큼 확장시킴   
    HashSet<Vector2Int> GetTilesInRange(Vector2Int center, int range) 
    {
        HashSet<Vector2Int> tilesInRange = new HashSet<Vector2Int>();
        for (int x = -range; x <= range; x++) 
        {
            for (int y = -range; y <= range; y++) 
            {
                Vector2Int tile = new Vector2Int(center.x + x, center.y + y);
                // 정수 거리 계산을 통해 범위 안에 있는 타일만 추가
                if (Mathf.Abs(tile.x - center.x) + Mathf.Abs(tile.y - center.y) <= range) 
                {
                    tilesInRange.Add(tile);
                }
            }
        }
        
        return tilesInRange;
    }
    
    private IEnumerator AttackCorotione()
    {   
        while(true)
        {
            // 기절
            if (unit.hp <= 0)
            {
                ChangeState(UnitState.Faint);
                yield break; // 코루틴 종료
            }

            var enemy = targetEnemy;
            Debug.Log($"{targetEnemy.name} AttackCorotione CheckAttackRange");

            // 공격 범위 확인
            if (!CheckAttackRange())
            {
                Debug.Log("[Unit] 공격 범위 내 적 없음, Move 상태로 전환");
                ChangeState(UnitState.Move);
                yield break; // 코루틴 종료
            }

            // 타겟이 없어 지거나 적이 쓰러진 경우
            if (enemy == null || enemy.GetComponent<Unit>().hp <= 0) // 적이 사라지거나 쓰러짐
            {
                Debug.Log($"{enemy?.name ?? "적"}이 쓰러졌습니다. Idle 상태로 전환");
                ChangeState(UnitState.Idle);
                yield break; // 코루틴 종료
            }
            //방향 전환
            Vector3 rotation = rect.localEulerAngles;
            if (enemy.transform.position.x <= transform.position.x)
            {
                rotation.y = 0; //왼쪽방향보게하기
            }
            else
            {
                rotation.y = 180; //오른쪽보게하기
            }

            animator.SetTrigger("Attack"); //공격 애니메이션
            yield return new WaitForSeconds(unit.attackInretval * 0.35f);
            //공격 범위에 따른 공격 실행
            if (unit.attackRange == 1)
            {
                PerformAttack(enemy);
            }
            else
            {
                SpawnProjectile(enemy);
            }
            
            yield return new WaitForSeconds(unit.attackInretval * 0.65f);        
        }
    }    
    
    // 근거리 공격 실행
    public void PerformAttack(GameObject enemy)
    {
        var enemyUnit = enemy.GetComponent<Unit>();
        if (enemyUnit == null)
            return;

        unit.DealDamage(enemyUnit);
    }

    // 원거리 공격 실행
    private void SpawnProjectile(GameObject enemy)
    {
        if (ProjectilePoolManager.Instance == null || projectileSpawnPoint == null)
            return;

        var proj = ProjectilePoolManager.Instance.Get(
            projectileType,
            projectileSpawnPoint.position,
            Quaternion.identity
        );

        if (proj != null)
        {
            proj.Init(this, enemy);
        }
    }

    private bool CheckAttackRange()
    {
        // 타일 중심에 도달했는지 확인
        if (!IsAtTileCenter())
            return false;

        // 현재 내 공격 가능 타일들
        HashSet<Vector2Int> tilesInRange = GetTilesInRange(currentTilePosition, unit.attackRange);

        if (targetEnemy != null)
        {
            Vector2Int enemyTilePosition = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);

            if (tilesInRange.Contains(enemyTilePosition))
            {
                Debug.Log($"[Unit] 기존 타겟 {targetEnemy.name}이(가) 공격 범위 내에 있습니다.");
                return true;
            }
        }

        string enemyTag = CompareTag("PlayerUnit") ? "EnemyUnit" : "PlayerUnit";

        GameObject best = null;
        int bestDist = int.MaxValue;

        var enemyList = CompareTag("PlayerUnit") ? tileMapManager.enemyUnits : tileMapManager.playerUnits;

        foreach (var go in enemyList)
        {
            if (go == null || !go.activeInHierarchy) continue;
            if (!go.CompareTag(enemyTag)) continue;

            Vector2Int pos = tileMapManager.GetTileFromWorldPosition(go.transform.position);
            if (!tilesInRange.Contains(pos)) continue;

            // 범위 안에 들어온 적들 중 가까운 적 우선
            int dist = Mathf.Abs(pos.x - currentTilePosition.x) + Mathf.Abs(pos.y - currentTilePosition.y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = go;
            }
        }

        if (best != null)
        {
            targetEnemy = best;
            return true;
        }

        Debug.Log($"{this.gameObject.name} [Unit] 공격 범위 내에 적이 없습니다.");
        return false;
    }

    // 이동 함수 (Ready 상태일때 UI로 이동하는 경우)
    public bool TryMoveBy(Vector2Int delta)
    {
        if(CurrentState != UnitState.Ready)
        {
            return false;
        }

        Vector2Int target = currentTilePosition + delta;

        if(target.x == 0)
        {
            Debug.Log("우리 진영이 아닙니다.");
            return false;
        }

        if(!tileMapManager.IsWalkable(target))
        {
            return false;
        }

        SetPositionInstant(target);
        return true;
    }

    //런 매니저에서 상태 변경용
    public void ForceReady()
    {
        StopAllCoroutines();
        currentState = UnitState.Ready;
    }

    public void ForceIdle()
    {
        StopAllCoroutines();
        currentState = UnitState.Idle;
        OnEnterIdle();
    }

    // 유닛 hp 0되었을때 
    public void OnDeath()
    {
        // 태그에 따라 처리
        if (CompareTag("EnemyUnit"))
        {
            gameObject.SetActive(false);
            RunManager.Instance.enemyUnits.Remove(gameObject);
            tileMapManager.enemyUnits.Remove(gameObject);
        }
        else if (CompareTag("PlayerUnit"))
        {
            gameObject.SetActive(false);
            RunManager.Instance.playerUnits.Remove(gameObject);
            tileMapManager.playerUnits.Remove(gameObject);
        }

        RunManager.Instance.CheckEndBattle();
    }
    
    // 타일 중심에 도달했는지 확인
    private bool IsAtTileCenter(float epsilon = 0.0004f)
    {
        Vector3 center = tileMapManager.tilemap.GetCellCenterWorld(
            new Vector3Int(currentTilePosition.x, currentTilePosition.y, 0)
        );
        return (transform.position - center).sqrMagnitude <= epsilon;
    }
}
