using System.Collections;
using System.Collections.Generic;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UnitFSM : MonoBehaviour
{
    public enum UnitState{ Idle, Move, Attack, Faint, Ready } // 유닛 상태 정의

    [SerializeField] private UnitState currentState; // 현재 상태
    public UnitState CurrentState => currentState; // 이동을 위한 읽기용
    [SerializeField] private RectTransform hudRoot; // HUD바 회전
    [SerializeField] private GameObject projectilePrefab; // 원거리공격투사체
    [SerializeField] private Transform projectileSpawnPoint; // 투사체 생성 위치

    public int unitId; // 유닛 고유 ID
    
    [Header("유닛 기본 속성")]
    Unit unit;

    // 유닛의 타일맵 관리자 참조
    private TileMapManager tileMapManager;
    // 현재 타일맵에서의 위치
    public Vector2Int currentTilePosition;
    private Vector2Int moveTargetTile;
    private bool hasMoveTarget;
    public GameObject targetEnemy;
    private RectTransform rect;
    private Animator animator;
    Rigidbody2D rb;

    private bool isMoving = false;

    public void Initialize(TileMapManager tileMapManager, Vector2Int initialPosition)
    {
        // 타일맵 관리자 참조 저장
        this.tileMapManager = tileMapManager;
        SetPositionInstant(initialPosition);
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

        Debug.Log($"[Unit] 초기 위치 설정: {transform.position} (중심: {tileCenter})");

        // 새 위치 타일 상태 업데이트
        tileMapManager.UpdateTileStatus(currentTilePosition);
    }

    void Awake()
    {
        unit = GetComponent<Unit>();
        unitId = GetInstanceID(); // 고유 ID를 Unity의 InstanceID로 설정
        currentState = UnitState.Idle; // 기본상태를 Idle // 추후 생성으로 옮겨야할듯  
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
            Debug.Log("[Unit] Move 상태에서 타겟이 사라짐, Idle 상태로 전환");
            ChangeState(UnitState.Idle);
            return;
        }

        Vector2Int enemyTile = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);
        if (unit.attackRange == 1) // 근접
        {
            moveTargetTile = GetBestAttackTile(enemyTile); // 적 옆 타일 중에서 하나
        }
        else
        {
            moveTargetTile = enemyTile; // 원거리는 일단 적 위치 기준
        }

        hasMoveTarget = true;
        
        StartCoroutine(MoveToCoroutine(moveTargetTile)); // 적의 위치로 이동
    }
    private void OnEnterAttack()
    {
        isMoving = false; // 이동 중지 플래그 초기화
        animator.SetFloat("Speed", 0f); //이동 애니메이션 종료
        if (targetEnemy == null)
        {
            Debug.Log("[Unit] 타겟이 죽었거나 사라짐, Idle 상태로 전환");
            ChangeState(UnitState.Idle);
            return;
        }

        Debug.Log("[Unit] OnEnterAttack 호출 - 공격 시작!");
        StartCoroutine(AttackCorotione(targetEnemy));
    }
    private void OnEnterFaint()
    {
        isMoving = false; // 이동 중지 플래그 초기화
        
        //animator.speed = 1f; // 애니메이션 속도 1로 복귀
        // 기절시 데이터 저장 후 오브젝트 삭제?
        // 적은 삭제 해야하는데 플레이어쪽은 삭제하면 안되는데
    }

    // IDLE 상태 
    private void HandleIdleState()
    {
        // 타겟이 없거나 기절한 경우 가장 가까운 적 찾기
        if(targetEnemy == null || targetEnemy.GetComponent<Unit>().hp <= 0)
        {
            targetEnemy = FindClosestEnemy();
        }

        if(targetEnemy != null)
        {
            if (CheckAttackRange())
            {
                Debug.Log("[Unit] 타겟이 공격 범위 내에 있음, Attack 상태로 전환");
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

    private Vector2Int GetBestAttackTile(Vector2Int enemyTilePosition)
    {
        // 적의 공격범위 (근접이니 서로 공격할수 있는 위치)
        HashSet<Vector2Int> tilesInRange = GetTilesInRange(enemyTilePosition, unit.attackRange);
        
        // 적이 서 있는 타일은 제외 (그 위에는 설 수 없음)
        tilesInRange.Remove(enemyTilePosition);

        Vector2Int bestTile = enemyTilePosition;
        int bestDist = int.MaxValue;

        foreach (var tile in tilesInRange)
        {
            // 내가 이미 그 자리에 서 있는 건 허용
            if (tile != currentTilePosition && !tileMapManager.IsWalkable(tile))
                continue;

            int dist = Mathf.Abs(tile.x - currentTilePosition.x) + Mathf.Abs(tile.y - currentTilePosition.y);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestTile = tile;
            }
        }

        // 결국 못가는 경우는 어쩔수 없이 타겟위치 호출
        return bestTile;
    }

    //캐릭터가 가야할 다음 타일을 보내는 용도
    private IEnumerator MoveToCoroutine(Vector2Int targetTile)
    {
        Debug.Log($"[Unit] MoveToCoroutine 시작 - isMoving: {isMoving}, currentTile: {currentTilePosition}, targetTile: {targetTile}");
        if (isMoving)
        {
            Debug.Log("[Unit] 이미 이동 중입니다. 코루틴 중단");
            yield break; // 이미 이동 중이면 실행하지 않음
        }
        
        // 서로 대각선일때 싸우지안는 문제 보정
        if (targetEnemy != null)
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
                else if (unit.totalAgility >= targetEnemy.GetComponent<Unit>().totalAgility)
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

        if (CheckAttackRange())
        {
            {
                ChangeState(UnitState.Attack);
                yield break;
            }
        }

        // 현재 타일과 목표 타일이 동일하면 이동 종료
        if (currentTilePosition == targetTile)
        {
            Debug.Log($"[Unit] 목표 타일 {targetTile}에 이미 도달.");
            ChangeState(UnitState.Idle);
            yield break;
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
            Debug.LogWarning($"[Unit] 경로를 찾을 수 없습니다! 시작: {currentTilePosition}, 목표: {targetTile}");
            ChangeState(UnitState.Idle);
            yield break;
        }

        isMoving = true;
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

        //Debug.Log($"[Unit] 다음 타일로 이동: {nextStep}");
        yield return StartCoroutine(FollowPath(nextStep));

        isMoving = false;

        // 다음 타일로 재귀 호출
        if (targetEnemy != null && currentState == UnitState.Move)
        {
            Vector2Int enemyTilePosition = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);
            yield return StartCoroutine(MoveToCoroutine(enemyTilePosition));
        }
        
    }    

    //다음 타일로 이동
    private IEnumerator FollowPath(Vector2Int targetTile)
    {
        // 타일의 월드 위치 계산
        Vector3 targetPosition = tileMapManager.tilemap.GetCellCenterWorld(new Vector3Int(targetTile.x, targetTile.y, 0));

        //방향 전환
        Vector3 rotation = rect.localEulerAngles;

        if (targetEnemy != null && targetEnemy.transform.position.x < transform.position.x)
        {
            rotation.y = 0; //왼쪽방향보게하기
        }
        else
        {
            rotation.y = 180; //오른쪽보게하기            
        }

        rect.localEulerAngles = rotation;

        if(hudRoot != null)
            hudRoot.localEulerAngles = rotation; // 체력 마나바 같이 회전문제 해결

        //이동 애니메이션
        animator.SetFloat("Speed", 1f);

        while(!tileMapManager.TryReserveTileForMove(targetTile, unitId))
        {
            // 대기 애니메이션
            animator.SetFloat("Speed", 0f);
            if (currentState != UnitState.Move)
            {
                yield break;
            }

            if (CheckAttackRange())
            {
                ChangeState(UnitState.Attack);
                yield break;
            }

            yield return null;
        }

        // 타겟 위치로 이동
        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            if (currentState != UnitState.Move)
            {
                Debug.Log("[Unit] 상태가 Move가 아니므로 이동 종료");
                yield break; // 상태가 Move가 아니면 이동 중단
            }

            rb.MovePosition(Vector3.MoveTowards(rb.position, targetPosition, Time.deltaTime * 3f));
            yield return new WaitForFixedUpdate();
        }

        // 위치 보정
        transform.position = targetPosition;

        // 현재 타일 갱신
        currentTilePosition = targetTile;
        // 예약 해제
        tileMapManager.ReleaseReservedTile(targetTile, unitId);

        // 이동 중에도 공격 범위 확인
        if (CheckAttackRange())
        {
            Debug.Log($"[Unit] 이동 중 적이 공격 범위 내에 들어옴, 이동 중지!");
            ChangeState(UnitState.Attack); // Attack 상태로 전환
            yield break; // 이동 종료
        }

        ChangeState(UnitState.Move);
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
    
    private IEnumerator AttackCorotione(GameObject enemy)
    {   
        while(true)
        {
            // 기절
            if (unit.hp <= 0)
            {
                ChangeState(UnitState.Faint);
                yield break; // 코루틴 종료
            }

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
        if (projectilePrefab == null || projectileSpawnPoint == null)
            return;

        GameObject proj = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        var projectile = proj.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.Init(this, enemy);
        }
    }

    private bool CheckAttackRange()
    {
        if (targetEnemy == null)
        {
            return false;
        }

        // 적의 타일 위치를 계산
        Vector2Int enemyTilePosition = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);

        // 현재 유닛의 공격 범위 내 타일을 가져옴
        HashSet<Vector2Int> tilesInRange = GetTilesInRange(currentTilePosition, unit.attackRange);

        // 적의 위치가 공격 범위 내 타일에 있는지 확인
        if (tilesInRange.Contains(enemyTilePosition))
        {
            Debug.Log($"[Unit] {targetEnemy.name}이(가) 공격 범위 내에 있습니다.");
            return true;
        }

        Debug.Log("[Unit] 공격 범위 내에 적이 없습니다.");
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
}
