using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RunState
{
    OnMap,          // 맵에서 다음 노드를 고르는 상태
    Ready,          // 전투, 이벤트 노드에서 선택지를 고르는 상태
    Battle,         // 전투중인 상태
    Reward,         // 라운드 클리어 후 보상, 상점 이용중
    Event,          // 이벤트 진행중
    Rest            // 휴식, 이것도 이벤트이긴한데 일단 넣어둠
}

// 런 상태 관리 클래스
public class RunManager : MonoBehaviour
{
    [Header("유닛 관련")]
    [SerializeField] private List<UnitData> playerUnitPool;       // 전체 유닛 풀
    [SerializeField] private List<UnitData> enemyUnitPool;       // 적 유닛 풀
    [SerializeField] private UnitSelectPanel unitSelectPanel; //캐릭터를 줄때 GainUnit()에서 사용
    [SerializeField] private ChooseUnitPanel chooseUnitPanel; //아이템을 줄 캐릭터 선택하는 패널
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private TileMapManager tileMapManager;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private RewardPhasePanel rewardPhasePanel;
    [SerializeField] private EnemySpawnManager enemySpawnManager;
    [SerializeField] private BiomeType currentBiome; // 지금 바이옴 (숲/평야 등)

    [Header("경험치 테이블")]
    [SerializeField] private float enemyExpFraction = 0.33f; // 적 경험치 계수
    [SerializeField] private double[] levelUpExpTable;       // 레벨업 필요 exp (공유)
    private double battleExpPool;                            // 이번 전투 누적 exp

    public static RunManager Instance { get; private set; }
    public RunState currentRunState {get; private set;} //초기 상태
    public int currentLevel; // 현재 진행중인 라운드
    public double gold; // 이벤트나 상점에서 사용되는 재화
    public List<GameObject> playerUnits = new List<GameObject>(); // 플레이어 캐릭터 리스트
    public List<GameObject> enemyUnits = new List<GameObject>();
    // 포메이션 저장용
    private Dictionary<int, Vector2Int> savedFormation = new Dictionary<int, Vector2Int>(); 
    
    public NodeType currentNodeType { get; private set; }

    public bool isInBattle; // 전투중인지 여부
    public bool isInEvent; // 이벤트 중인지 여부
    public bool isInReward; // 보상 선택 중인지 여부

    // 아이템 변수
    public int levelPotionBonus; // 경험의서 (경험비약의 효율을 1씩 올려줌)
    public int expAmulet;        // 경험부적 (경험치 획득 효율 증가 1개당 25%)
    private WeatherType currentWeather = WeatherType.None;
        
    // 싱글톤 (다른씬에 넘어갈일은 없지만 일단 만들어둠)
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        StartNewRun();
    }

    void StartNewRun()
    {
        currentLevel = 1;
        gold = 1000; // 초기 골드 설정
        playerUnits.Clear();
        EnsureLevelUpExpTable(); // 경험치 테이블 초기화
        battleExpPool = 0;  // 전투 경험치 초기화
        isInBattle = false;
        isInEvent = false;
        isInReward = false;                
        currentRunState = RunState.OnMap; // 초기에 지도 부터 보여준다.
        currentBiome = BiomeType.Forest;  // 숲에서 시작
        //튜토리얼 기능 추가시 작성

        // 기본 유닛 하나 추가 후
        GainUnit();
        // 맵생성 함수 호출

        //맵열기 mapGenerator.ToggleMapView();
    }

    public void SelectNode(MapNode node) // Map에서 노드를 클릭할때 호출
    {
        currentNodeType = node.Type;
        currentLevel = node.Level + 1;

        // 선택된 노드에 따라 이벤트 처리
        switch (currentNodeType)
        {
            case NodeType.Combat:
                mapGenerator.ToggleMapView();
                EnterReady();                
                break;
            case NodeType.Boss:
                mapGenerator.ToggleMapView();
                EnterReady();
                // 보스 시작전 대화 하고 전투 노드랑 똑같이 작동
                break;
            case NodeType.Event:
                // 이벤트 시작 로직
                // 이벤트 종류에 따라 분기 처리 필요
                break;
            case NodeType.Rest:
                // 보상 선택 로직
                break;
            default:
                break;
        }
    }

    // 전투 노드 관련 함수
    // 전투 준비 상태로 진입
    void EnterReady()
    {
        currentRunState = RunState.Ready;
        isInBattle = false;
        
        enemyUnits.Clear();
        tileMapManager.enemyUnits.Clear();
        // 전투 준비 상태로 진입시 적 스폰
        enemySpawnManager.SpawnBattle(currentBiome, currentLevel, currentNodeType == NodeType.Boss);

        AllUnitsReady();

        //전투 시작 버튼 활성화 구현

        // 전투 경험치 초기화
        battleExpPool = 0;
        EnsureLevelUpExpTable();
    }

    void EnterBattle()
    {
        // 대기상태에서 전투하기 버튼 선택하면
        isInBattle = true; 
        currentRunState = RunState.Battle;

        if (currentNodeType == NodeType.Boss)
            enemySpawnManager.SpawnBossBattle(currentBiome, currentLevel);
        else
            enemySpawnManager.SpawnNormalBattle(currentBiome, currentLevel);

        AllUnitsIdle();
    }
    
    public void StartBattle() // 전투 시작 버튼 누르면 호출
    {
        if (currentRunState != RunState.Ready)
            return;

        SavePlayerFormation();

        isInBattle = true;
        currentRunState = RunState.Battle;

        AllUnitsIdle();
    }

    public void BattleTest() // 나중에 삭제
    {
        enemySpawnManager.SpawnNormalBattle(currentBiome, currentLevel);
    }

    void EndBattle(bool isWin)
    {
        if (isWin)
        {
            AwardBattleExpToParty();
            RestorePlayerFormation();
            EnterReward();
        }
        else
        {
            // TODO: 게임 오버 처리
            // 게임 오버 UI 구현
            // 게임 재시작 또는 메인메뉴로 돌아가기 구현
        }
    }

    public void OnEnemyDefeated(GameObject enemyGO)
    {
        if (enemyGO == null) return;

        var enemyUnit = enemyGO.GetComponent<Unit>();
        if (enemyUnit != null)
        {
            // 적 레벨 기반으로 경험치 계산
            double baseReward = GetRequiredExp(enemyUnit.level) * enemyExpFraction;
            battleExpPool += baseReward;
        }

        // 리스트에서 제거
        enemyUnits.Remove(enemyGO);
        if (tileMapManager != null) tileMapManager.enemyUnits.Remove(enemyGO);

        // 타일 점유 해제는 네 기존 “죽음 처리”에서 하던 방식대로 유지
        Destroy(enemyGO);

        // 승리 체크
        CheckEndBattle();
    }

    private void AwardBattleExpToParty()
    {
        if (battleExpPool <= 0) return;

        // 기절자 제외: hp > 0 만
        var receivers = new List<Unit>();
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            if (u.hp <= 0) continue; // 기절 제외(네 의도)
            receivers.Add(u);
        }

        if (receivers.Count == 0) return;

        double per = battleExpPool / receivers.Count;

        // 경험부적: 1개당 25%
        double relicMul = 1.0 + 0.25 * expAmulet;

        foreach (var u in receivers)
        {
            u.GainExp(per * relicMul);
        }

        battleExpPool = 0;
    }

    void EnterReward()
    {
        isInReward = true;
        currentRunState = RunState.Reward;
        // 보상 UI 구현
        // 선택후 다음라운드 진행 구현 
        // Map 다시 열기
        GiveReward();
    }

    public void GiveReward()
    {
        Debug.Log("보상실행");
        int rewardCount = 3;

        var rewardChoices = rewardManager.GetRewardChoices(currentLevel, rewardCount);
        var shopChoices   = rewardManager.GetShopItems(currentLevel);

        rewardPhasePanel.Open(
            rewardChoices,
            shopChoices,
            OnRewardSelected,   // 무료 보상
            OnShopItemClicked   // 상점 아이템
        );
    }

    // 보상은 선택시 바로 다음라운드 진행
    private void OnRewardSelected(RewardDefinition reward)
    {
        OnRewardClicked(reward);

        isInReward = false;
        GoToNextRound();
    }

    //상점은 몇번이고 이용가능
    private void OnShopItemClicked(RewardDefinition reward)
    {
        if (gold < reward.shopPrice)
        {
            Debug.Log("골드 부족!");
            return;
        }

        gold -= reward.shopPrice;

        // 아이템 효과 적용
        OnRewardClicked(reward);
    }

    // (보상선택후)라운드 끝 -> 다음라운드 시작전 까지 해야될 동작
    public void GoToNextRound()
    {
        // 다음 라운드로 넘어가는 준비
        currentLevel++;

        // 맵 다시 열기
        mapGenerator.ToggleMapView();
        currentRunState = RunState.OnMap;        
    }

    // 플레이어에게 유닛을 제공하는 함수
    public void GainUnit()
    {
        List<UnitData> candidates = GetRandomUnits(3);
        unitSelectPanel.Open(candidates, OnUnitSelected);
    }

    private List<UnitData> GetRandomUnits(int count)
    {
        var list = new List<UnitData>(playerUnitPool); // 플레이어블 유닛만 있음

        // 셔플
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }

        if (list.Count > count)
            list.RemoveRange(count, list.Count - count);

        return list;
    }

    private void OnUnitSelected(UnitData selected)
    {
        SpawnUnit(selected);
        // 캐릭터 선택 후 맵 열기
        if(RunState.OnMap == currentRunState)
        {
            mapGenerator.ToggleMapView();
        }
       
    }

    private void SpawnUnit(UnitData data)
    {
        // 프리팹 인스턴스 생성
        GameObject unit = Instantiate(data.prefab);

        // 시작 위치 (빈자리를 찾는 함수 필요)
        Vector2Int startTile;
        tileMapManager.GetEmptyTile(out startTile);

        // UnitFSM 초기화
        UnitFSM fsm = unit.GetComponent<UnitFSM>();
        fsm.Initialize(tileMapManager, startTile);

        Unit unitComp = unit.GetComponent<Unit>();
        if (unitComp != null)
        {
            // UnitData에서 Unit으로 변수를 보내줌
            unitComp.ApplyData(data);
        }

        // 런/타일맵에 등록
        playerUnits.Add(unit);
        tileMapManager.playerUnits.Add(unit);
    }
   
    //보상관련 함수
    public void OnRewardClicked(RewardDefinition reward)
    {
        switch (reward.targetType)
        {
            case RewardTargetType.None:
                ApplyRewardNoTarget(reward); //런매니저에 바로 적용
                break;

            case RewardTargetType.ChooseUnit:
                OpenEquipToUnitUI(reward);   //캐릭터가 장착, 소지
                break;

            case RewardTargetType.RandomUnit:
                ApplyRewardToRandomUnit(reward); //랜덤캐릭터에 효과 적용
                break;
        }
    }

    // 유닛 선택 필요 없는 보상
    private void ApplyRewardNoTarget(RewardDefinition reward)
    {
        switch (reward.rewardType)
        {
            case RewardType.Gold:
                gold += reward.goldAmount;
                Debug.Log($"골드 +{reward.goldAmount}, 현재 골드: {gold}");
                break;

            case RewardType.WeatherChange:
                currentWeather = reward.weatherType;
                Debug.Log($"날씨 변경: {currentWeather}");
                break;

            case RewardType.InstantHeal:
                // 파티 전체 회복
                foreach (var unitGO in playerUnits)
                {
                    var unit = unitGO.GetComponent<Unit>();
                    if (unit == null) continue;

                    unit.HealByPotion(
                        reward.healAmount,
                        reward.healProportion,
                        reward.fullHeal
                    );
                }
                break;

            case RewardType.InstantExp:
                foreach (var unitGO in playerUnits)
                {
                    var unit = unitGO.GetComponent<Unit>();
                    if (unit == null) continue;

                    unit.GainLevel(reward.levelIncrease + levelPotionBonus);
                }
                break;
            
            case RewardType.Relic:
                //Unit이 아닌 RunManager의 변수에 영향
                levelPotionBonus += reward.levelPotionBonus;
                expAmulet += reward.expAmulet;
                break;

            default:
                Debug.LogWarning($"RewardType {reward.rewardType} 는 타겟이 필요하거나 아직 미구현");
                break;
        }
    }
    
    //캐릭터 하나에게 적용하는 아이템
    private void OpenEquipToUnitUI(RewardDefinition reward)
    {
        if (playerUnits.Count == 0)
        {
            Debug.LogWarning("플레이어 유닛이 없어서 보상을 적용할 수 없음.");
            return;
        }

        var unitList = new List<Unit>();
        foreach (var unitGO in playerUnits)
        {
            var unit = unitGO.GetComponent<Unit>();
            if (unit != null)
                unitList.Add(unit);
        }

        if (unitList.Count == 0)
        {
            Debug.LogWarning("플레이어 유닛에 Unit 컴포넌트가 없습니다.");
            return;
        }

        chooseUnitPanel.Open(unitList, (Unit selectedUnit) =>
        {
            ApplyRewardToUnit(reward, selectedUnit);
        });
    }

    //랜덤으로 적용하는 아이템
    private void ApplyRewardToRandomUnit(RewardDefinition reward) // 인자에 인덱스를 넣는거 고려
    {
        if (playerUnits.Count == 0)
        {
            Debug.LogWarning("플레이어 유닛이 없어서 랜덤 보상을 적용할 수 없음.");
            return;
        }

        int idx = UnityEngine.Random.Range(0, playerUnits.Count);
        GameObject targetGO = playerUnits[idx];
        Unit target = targetGO.GetComponent<Unit>();

        if (target == null)
        {
            Debug.LogWarning("랜덤으로 뽑은 오브젝트에 Unit 컴포넌트가 없습니다.");
            return;
        }

        ApplyRewardToUnit(reward, target);
    }

    private void ApplyRewardToUnit(RewardDefinition reward, Unit unit)
    {
        switch (reward.rewardType)
        {
            case RewardType.Equipment:
                unit.Equip(reward.equipment);
                break;

            case RewardType.InstantHeal:
                unit.HealByPotion(
                    reward.healAmount,
                    reward.healProportion,
                    reward.fullHeal
                );
                break;

            case RewardType.InstantExp:
                unit.GainLevel(reward.levelIncrease + levelPotionBonus);
                break;

            case RewardType.PassiveItem:
                unit.AddPassiveItem(reward);
                break;

            default:
                Debug.LogWarning($"RewardType {reward.rewardType} 는 Unit 대상 적용이 아직 구현되지 않음");
                break;
        }
    }

    // 전투노드 관련함수
    void SavePlayerFormation() // 전투 준비 상태에서 포메이션 저장
    {
        savedFormation.Clear();

        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            savedFormation[fsm.unitId] = fsm.currentTilePosition;
        }
    }

    void AllUnitsReady()
    {
        // 아군 유닛
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            fsm.ForceReady();
        }

        // 적 유닛
        foreach (var go in enemyUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            fsm.ForceReady();
        }
    }

    // StartBattle에서 호출
    void AllUnitsIdle()
    {
        //아군 유닛
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            fsm.ForceIdle();
        }

        //적 유닛
        foreach (var go in enemyUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            fsm.ForceIdle();
        }
    }

    public void CheckEndBattle()
    {
        if (!isInBattle) return;

        if (enemyUnits.Count == 0)
        {
            isInBattle = false;
            EndBattle(true);
        }
        else if (playerUnits.Count == 0)
        {
            isInBattle = false;
            EndBattle(false);
        }
    }

    void RestorePlayerFormation()
    {
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            if (savedFormation.TryGetValue(fsm.unitId, out var tile))
            {
                fsm.SetPositionInstant(tile);
                fsm.ForceReady(); // 다음 전투 준비 상태로
            }
        }
    }

    // 경험치 테이블 초기화
    private void EnsureLevelUpExpTable()
    {
        if (levelUpExpTable != null && levelUpExpTable.Length == Unit.maxLevel + 1) return;

        levelUpExpTable = new double[Unit.maxLevel + 1];
        double baseExp = 80;
        double growthPow = 1.08;
        double offset = 20;

        for (int lv = 1; lv <= Unit.maxLevel; lv++)
        {
            double req = baseExp * System.Math.Pow(growthPow, lv - 1) + offset * lv;
            levelUpExpTable[lv] = System.Math.Round(req);
        }
    }

    public double GetRequiredExp(int level)
    {
        EnsureLevelUpExpTable();
        int lv = Mathf.Clamp(level, 1, Unit.maxLevel);
        return levelUpExpTable[lv];
    }
}
