using System.Collections;
using System.Collections.Generic;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UnitFSM : MonoBehaviour
{
    public enum UnitState{ Ready, Idle, Move, Attack, Faint, Stun } // 유닛 상태 정의

    [SerializeField] private UnitState currentState; // 현재 상태
    public UnitState CurrentState => currentState; // 이동을 위한 읽기용
    [SerializeField] private RectTransform hudRoot; // HUD바 회전
    [SerializeField] private ProjectileType projectileType; // 투사체 타입
    [SerializeField] private Transform projectileSpawnPoint; // 투사체 생성 위치
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0.2f, 0.2f, 0f);// 투사체 생성 위치 오프셋
    [SerializeField] private Transform aimPoint; // 투사체 조준점
    [SerializeField] private UnitGridAgent gridAgent;
    [SerializeField] private UnitHUDSpawner hudSpawner;

    public Transform AimPoint => aimPoint;
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
    private int _lastStateChangeFrame = -1;
    private bool _deathHandled = false; // 죽음 처리 중복 방지
    private Coroutine stunCo; // 기절 코루틴 참조

    // 디버프 상태 플래그
    public bool isBurning = false;
    
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
        tileMapManager.SetTileStatus(currentTilePosition, -1);
    }

    void Awake()
    {
        unit = GetComponent<Unit>();
        unitId = GetInstanceID(); // 고유 ID를 Unity의 InstanceID로 설정
        currentState = UnitState.Ready; // 기본상태를 Ready
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        rect = GetComponent<RectTransform>(); //회전에 이용할 컴포넌트초기화
        hudSpawner = GetComponent<UnitHUDSpawner>();

        if (aimPoint == null)
        {
            //UnitRoot/Root/BodySet
            var t = transform.Find("UnitRoot/Root/BodySet");
            if (t != null) aimPoint = t;
        }

        if (aimPoint == null)
        {
            // HorseRoot/Pivot_Main/Pivot_Body
            var t = transform.Find("HorseRoot/Pivot_Main/Pivot_Body");
            if (t != null) aimPoint = t;
        }

        if (aimPoint == null)
            aimPoint = transform;

        if (projectileSpawnPoint == null)
            projectileSpawnPoint = aimPoint;
        
                  
    }
    
    void Start()
    {
        tileMapManager = FindObjectOfType<TileMapManager>();
        FixHudFacing();
    }
    
    void Update()
    {
        if (!_deathHandled && unit != null && unit.hp <= 0)
        {
            _deathHandled = true;
            ChangeState(UnitState.Faint);
            return; // 아래 로직 실행하지 않게(상태 튐 방지)
        }

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

        // 체력 재생
        if (!(currentState == UnitState.Ready || currentState == UnitState.Faint))
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
        if (_lastStateChangeFrame == Time.frameCount) return;
        _lastStateChangeFrame = Time.frameCount;
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

        // 타겟이 없거나 기절한 경우 가장 가까운 적 찾기
        if (targetEnemy == null || !targetEnemy.activeInHierarchy || targetEnemy.GetComponent<Unit>().hp <= 0)
        {
            targetEnemy = FindClosestEnemy();
        }

        if(targetEnemy != null)
        {   
            if (CheckAttackRange())
            {
                //Debug.Log($"[Unit] 타겟이 공격 범위 내에 있음, Attack 상태로 전환");                
                ChangeState(UnitState.Attack);
            }
            else
            {
                //Debug.Log("[Unit] 타겟 발견, Move 상태로 전환");
                ChangeState(UnitState.Move); // 공격 범위에 없으면 Move 상태로 전환
            }             
        }
        else
        {
            //Debug.Log("[Unit] 타겟 없음, 대기 중");
        }
    }
    private void OnEnterMove()
    {
        isMoving = true;
        //animator.speed = 1f; // 애니메이션 속도 1로 복귀
        //타겟없을때 State변경
        if (targetEnemy == null)
        {
            ChangeState(UnitState.Idle);
            return;
        }

        animator.SetFloat("Speed", 1f); //이동 애니메이션 시작
        
        Vector2Int enemyTile = GetEnemyTile(targetEnemy);

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
        isMoving = false;

        StartCoroutine(FaintRoutine());
    }

    private void OnEnterStun()
    {
        
    }
    private void HandleIdleState()
    {
        // 타겟이 없거나 기절한 경우 가장 가까운 적 찾기
        if(targetEnemy == null || !targetEnemy.activeInHierarchy || targetEnemy.GetComponent<Unit>().hp <= 0)
        {
            targetEnemy = FindClosestEnemy();
        }

        if (CheckAttackRange())
            ChangeState(UnitState.Attack);
        else
            ChangeState(UnitState.Move);
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
        
        if (gridAgent != null)
            gridAgent.SetTarget(targetEnemy);
    }

    private void HandleAttackState()
    {
        
    }

    // FAINT 상태
    private void HandleFaintState()
    {        
        if(unit.hp > 1)
        {
            Debug.Log($"[Unit] 부활!!!");
            // 캐릭터 재생성? 아니면 다른곳에 뒀다가 부활?
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

            Vector2Int enemyTilePosition = GetEnemyTile(enemy);
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
        while (true)
        {
            // 내가 죽었으면 종료
            if (unit.hp <= 0)
            {
                ChangeState(UnitState.Faint);
                yield break;
            }

            // 범위 밖이면 이동
            if (!CheckAttackRange())
            {
                ChangeState(UnitState.Move);
                yield break;
            }

            // CheckAttackRange가 targetEnemy를 바꿀 수 있으니, 여기서 다시 잡는다
            var enemy = targetEnemy;

            // 타겟이 사라졌거나 죽었으면 Idle로
            if (!enemy || !enemy.activeInHierarchy ||
                !enemy.TryGetComponent<Unit>(out var enemyUnit) || enemyUnit.hp <= 0)
            {
                targetEnemy = FindClosestEnemy();
                if (gridAgent != null) gridAgent.SetTarget(targetEnemy);

                if (targetEnemy == null)
                {
                    TryChangeState(UnitState.Idle);
                    yield break;
                }

                continue;
            }

            // 방향 전환 (FSM도 TryGetComponent로 안전하게)
            if (enemy.TryGetComponent<UnitFSM>(out var efsm))
            {
                if (efsm.currentTilePosition.x <= currentTilePosition.x) FlipLeft();
                else FlipRight();
            }

            animator.SetTrigger("Attack");
            float interval = unit.EffectiveAttackInterval;
            // 타격 타이밍 대기
            yield return new WaitForSeconds(unit.attackInretval * 0.35f);

            // 여기서 다시 한 번 "지금도 살아있는지" 확인
            enemy = targetEnemy;
            if (!enemy || !enemy.activeInHierarchy ||
                !enemy.TryGetComponent<Unit>(out enemyUnit) || enemyUnit.hp <= 0)
            {
                targetEnemy = null;
                if (gridAgent != null) gridAgent.SetTarget(null);
                ChangeState(UnitState.Idle);
                yield break;
            }

            // 공격 실행 
            var skills = GetComponent<UnitSkillSystem>();
            bool casted = (skills != null) && skills.TryCastFullManaSkill(enemy);
            // 마나가 가득차면 스킬사용 그렇지 않으면 일반공격
            if (!casted)
            {
                if (unit.attackRange == 1) PerformAttack(enemy);
                else SpawnProjectile(enemy);
            }

            // 후딜
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

    public Vector3 GetProjectileSpawnWorldPos()
    {
        // 기준점(몸통)
        var basePos = (aimPoint != null) ? aimPoint.position : transform.position;

        // 현재 바라보는 방향 판단:
        // rect.localEulerAngles.y 가 180이면 오른쪽, 0이면 왼쪽
        bool facingRight = false;
        if (rect != null)
        {
            float y = rect.localEulerAngles.y;
            // 180 근처면 오른쪽
            facingRight = Mathf.Abs(Mathf.DeltaAngle(y, 180f)) < 1f;
        }

        // 오프셋 X 플립
        Vector3 off = projectileSpawnOffset;
        if (facingRight)
            off.x = -off.x;

        return basePos + off;
    }

    // 공격 범위 내에 적이 있는지 확인
    public bool CheckAttackRange()
    {
        // 타겟 유효성 정리
        if (targetEnemy != null)
        {
            if (!targetEnemy.activeInHierarchy ||
                (targetEnemy.TryGetComponent<Unit>(out var tu) && tu.hp <= 0))
            {
                targetEnemy = null;
                if (gridAgent != null) gridAgent.SetTarget(null);
            }
        }

        // 타일 센터 아니면 판정 안 함(기존 유지)
        if (!IsAtTileCenter()) return false;

        int range = unit.attackRange;

        // 1) 기존 타겟이 범위면 바로 true
        if (targetEnemy != null)
        {
            Vector2Int tpos = GetEnemyTile(targetEnemy);
            if (Manhattan(currentTilePosition, tpos) <= range)
                return true;
        }

        // 2) 범위 내 후보 중 가장 가까운 적을 다시 선택
        string enemyTag = CompareTag("PlayerUnit") ? "EnemyUnit" : "PlayerUnit";
        var enemyList = CompareTag("PlayerUnit") ? tileMapManager.enemyUnits : tileMapManager.playerUnits;

        GameObject best = null;
        int bestDist = int.MaxValue;

        foreach (var go in enemyList)
        {
            if (go == null || !go.activeInHierarchy) continue;
            if (!go.CompareTag(enemyTag)) continue;

            // 살아있는지 체크
            var eu = go.GetComponent<Unit>();
            if (eu == null || eu.hp <= 0) continue;

            Vector2Int pos = GetEnemyTile(go);
            int dist = Manhattan(currentTilePosition, pos);

            if (dist <= range && dist < bestDist)
            {
                bestDist = dist;
                best = go;
            }
        }

        if (best != null)
        {
            targetEnemy = best;
            // GridAgent도 타겟 동기화(권장)
            if (gridAgent != null) gridAgent.SetTarget(best);
            return true;
        }

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
        if(_deathHandled)
            return;

        StopAllCoroutines();
        
        isMoving = false;
        animator.SetFloat("Speed", 0);

        currentState = UnitState.Ready;
        
        //태그가 아군일때 FlipRight
        if (this.CompareTag("PlayerUnit"))
        {
            FlipRight();
        }
        else
        {
            FlipLeft();
        }
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
        Vector2Int pos = gridAgent != null ? gridAgent.TilePos : currentTilePosition;
        tileMapManager.SetTileStatus(pos, 0);

        // 태그에 따라 처리
        if (CompareTag("EnemyUnit"))
        {
            gameObject.SetActive(false);
            RunManager.Instance.enemyUnits.Remove(gameObject);
            tileMapManager.enemyUnits.Remove(gameObject);
            Destroy(gameObject);
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

    // 유닛 스프라이트 회전
    private void FixHudFacing()
    {
        var hudT = hudSpawner != null ? hudSpawner.HudTransform : null;
        if (hudT == null) return;

        float y = rect.localEulerAngles.y; // 0 또는 180

        var r = hudT.localEulerAngles;
        r.y = y;                  // HUD는 항상 정면 유지
        hudT.localEulerAngles = r;
    }

    public void FlipLeft()
    {
        Vector3 rotation = rect.localEulerAngles;
        rotation.y = 0; //왼쪽방향보게하기
        rect.localEulerAngles = rotation;
        FixHudFacing();
    }

    public void FlipRight()
    {
        Vector3 rotation = rect.localEulerAngles;
        rotation.y = 180f; //오른쪽방향보게하기
        rect.localEulerAngles = rotation;
        FixHudFacing();
    }
    
    public bool TryChangeState(UnitState next)
    {
        if (CurrentState == next) return false;

        // 예시 가드(필요한 것만)
        if (CurrentState == UnitState.Faint) return false;
        if (CurrentState == UnitState.Stun && next == UnitState.Attack) return false;

        ChangeState(next);
        return true;
    }

    private Vector2Int GetEnemyTile(GameObject enemy)
    {
        if (enemy == null) return currentTilePosition;

        // 1순위: 그리드 에이전트의 논리 타일
        var ag = enemy.GetComponent<UnitGridAgent>();
        if (ag != null) return ag.TilePos;

        // 2순위: FSM이 들고 있는 타일
        var fsm = enemy.GetComponent<UnitFSM>();
        if (fsm != null) return fsm.currentTilePosition;

        // 최후: 월드좌표
        return tileMapManager.GetTileFromWorldPosition(enemy.transform.position);
    }

    // 맨해튼 거리 계산
    private int Manhattan(Vector2Int a, Vector2Int b)
    => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // 상태이상
    public void ApplyStun(float duration)
    {
        if (stunCo != null) StopCoroutine(stunCo);
        stunCo = StartCoroutine(StunRoutine(duration));        
    }

    private IEnumerator StunRoutine(float duration)
    {
        animator.SetFloat("Speed", 0f); //이동 애니메이션 종료
        isMoving = false; // 이동 중지 플래그 초기화
        animator.SetTrigger("Stun");               
        yield return new WaitForSeconds(duration);

        ChangeState(UnitState.Idle);       // 전투 복귀
    }

    private IEnumerator FaintRoutine()
    {
        // 기절 애니메이션
        animator.SetTrigger("Faint");

        // Faint 상태로 들어갈 때까지 대기
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("4_Death"))
            yield return null;

        // 끝날 때까지 대기 (normalizedTime 1 = 100%)
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        // 현재 점유 타일 비우기
        Vector2Int pos = gridAgent != null ? gridAgent.TilePos : currentTilePosition;
        tileMapManager.SetTileStatus(pos, 0);
        
        if (this.CompareTag("EnemyUnit"))
        {
            RunManager.Instance.OnEnemyDefeated(gameObject);
            yield break;
        }

        if(this.CompareTag("PlayerUnit"))
        {
            gameObject.SetActive(false);
            RunManager.Instance.CheckEndBattle();
            yield break;
        }
    }
    // 부활함수
    public void ReviveToEmptyTile(bool halfHeal)
    {
        unit.HealByPotion(0f, 0.5f, !halfHeal);

        //기절 상태가 아니면 회복하고 끝
        if(!_deathHandled)
            return;
        
        // 빈 타일 찾기
        Vector2Int tile;
        tileMapManager.GetEmptyTile(out tile);

        // 위치/점유 동기화 (여기서 SetTileStatus(tile, -1)까지 처리됨)
        SetPositionInstant(tile);

        // 다시 등장
        gameObject.SetActive(true);

        animator.Rebind();
        animator.Update(0f);
        //애니메이션 초기화
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Stun");
        animator.SetFloat("Speed", 0f);

        animator.Play("ResetPose", 0, 0f);
        animator.Update(0f);

        animator.Play("0_Idle", 0, 0f);

        // 상태도 정상화
        _deathHandled = false;
        ChangeState(UnitState.Ready);
    }
}
