using System.Collections;
using System.Collections.Generic;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Unit : MonoBehaviour
{
    public enum UnitState{ Idle, Move, Attack, Faint }

    [SerializeField] private UnitState currentState; // 현재 상태
    [SerializeField] private RectTransform hudRoot; // HUD바 회전

    public int unitId; // 유닛의 ID 경로 예약할때 사용

    public int level = 0;
    public int maxLevel = 200;
    public float maxHp = 100;
    public float hp = 100;    
    public float maxMp = 200;
    public float mp;
    public int exp = 0;
    List<int> Equipment = new List<int>();
    public float[] levelUpExp = new float[199];
    public int attackRange = 1;
    public int strength;
    public int agility;
    public int intelligence;
    //strength 보너스 스탯
    public float BonusmaxHp;
    public float hpRecovery;
    //agility 보너스스탯
    public float attackInretval = 1f;
    public float criticalProbability;
    //intelligence 보너스스탯
    public float manaRecovery;
    public float bonusExp;

    // 유닛의 타일맵 관리자 참조
    private TileMapManager tileMapManager;
    // 현재 타일맵에서의 위치
    public Vector2Int currentTilePosition;
    public Vector2Int targetTilePosition;
    public GameObject targetEnemy;
    private RectTransform rect;
    private Animator animator;
    Rigidbody2D rb;

    private bool isMoving = false;

    public void Initialize(TileMapManager tileMapManager, Vector2Int initialPosition)
    {
        // 타일맵 관리자 참조 저장
        this.tileMapManager = tileMapManager;

        // 유닛 초기 위치 설정
        currentTilePosition = initialPosition;

        // 유닛의 월드 좌표 동기화
        Vector3Int initialPosition3D = new Vector3Int(initialPosition.x, initialPosition.y, 0);
        Vector3 tileCenter = tileMapManager.tilemap.GetCellCenterWorld(initialPosition3D);

        // 유닛 위치 설정
        transform.position = tileCenter;

        Debug.Log($"[Unit] 초기 위치 설정: {transform.position} (중심: {tileCenter})");
    }

    private void Start()
    {
        tileMapManager = FindObjectOfType<TileMapManager>();
        
    }

    void Awake()
    {
        unitId = GetInstanceID(); // 고유 ID를 Unity의 InstanceID로 설정
        currentState = UnitState.Idle; // 기본상태를 Idle // 추후 생성으로 옮겨야할듯  
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        rect = GetComponent<RectTransform>(); //회전에 이용할 컴포넌트초기화
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

        Vector2Int enemyTilePosition = tileMapManager.GetTileFromWorldPosition(targetEnemy.transform.position);
        MoveTo(enemyTilePosition); // 적의 위치로 이동
    }
    private void OnEnterAttack()
    {
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
        //animator.speed = 1f; // 애니메이션 속도 1로 복귀
        // 기절시 데이터 저장 후 오브젝트 삭제?
        // 적은 삭제 해야하는데 플레이어쪽은 삭제하면 안되는데
    }

    // IDLE 상태 
    private void HandleIdleState()
    {
        if(targetEnemy == null)
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
        
        if(hp > 1)
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

    public void LevelUp()
    {
        if(exp > levelUpExp[level] && level < maxLevel)
        {
            level++;
            // 레벨업시 능력치 추가
        }
    }

    public void Equip()
    {
        //장비장착
        //장비 리스트에서 장비아이템 삭제후 캐릭터의 장비리스트에 추가
    }

    public void UnEquip()
    {
        //장비 해체
        //캐릭터의 장비리스트에서 아이템 삭제 후 장비리스트에 추가
    }

    public void MoveTo(Vector2Int targetTile)
    {
        StartCoroutine(MoveToCoroutine(targetTile));
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
        isMoving = true;

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
            if (tileData.Status == -1 && tileData.Position != currentTilePosition)
            {
                occupiedTiles.Add(tileData.Position);
            }
        }

        List<Vector2Int> path = AStarPathfinder.FindPath(currentTilePosition, targetTile, tileMapManager, occupiedTiles);

        if (path.Count == 0)
        {
            Debug.LogWarning($"[Unit] 경로를 찾을 수 없습니다! 시작: {currentTilePosition}, 목표: {targetTile}");
            yield break;
        }

        // 첫 번째 타일로 이동
        Vector2Int nextStep = path[1];
        Debug.Log($"[Unit] 다음 타일로 이동: {nextStep}");
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
        hudRoot.localEulerAngles = rotation; // 체력 마나바     

        //이동 애니메이션
        animator.SetFloat("Speed", 1f);

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
        return -2 - unitId; // 고유 예약 상태
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

        if (this.CompareTag("EnemyUnit"))
        {
            targetList = tileMapManager.playerUnits;
        }

        foreach (GameObject enemy in targetList)
        {
            if (enemy == null) continue;

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
            if (hp <= 0)
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
            yield return new WaitForSeconds(attackInretval * 0.9f);
            PerformAttack(enemy);
            
            yield return new WaitForSeconds(attackInretval * 0.1f);        
        }
    }    
    
    private void PerformAttack(GameObject enemy)
    {
        // 공격 실행
        Debug.Log($"{enemy.name}를 공격");
        enemy.GetComponent<Unit>().hp -= 10; // 공격력만큼 체력 감소 테스트용으로 10
        Debug.Log($"{enemy.name}의 남은 체력: {enemy.GetComponent<Unit>().hp}");
        
        // 마나 회복
        float manaGain = 10f; // 공격 시 회복할 마나 양
        mp = Mathf.Min(mp + manaGain, maxMp); // maxMp를 초과하지 않도록 제한
        Debug.Log($"[Unit] 마나 회복: {manaGain}, 현재 마나: {mp}/{maxMp}");
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
        HashSet<Vector2Int> tilesInRange = GetTilesInRange(currentTilePosition, attackRange);

        // 적의 위치가 공격 범위 내 타일에 있는지 확인
        if (tilesInRange.Contains(enemyTilePosition))
        {
            Debug.Log($"[Unit] {targetEnemy.name}이(가) 공격 범위 내에 있습니다.");
            return true;
        }

        Debug.Log("[Unit] 공격 범위 내에 적이 없습니다.");
        return false;
    }
}
