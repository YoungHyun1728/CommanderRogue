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
}

// 런 상태 관리 클래스
public partial class RunManager : MonoBehaviour
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
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private BiomeType currentBiome; // 지금 바이옴 (숲/평야 등)

    [Header("바이옴")]
    [SerializeField] private BiomeType fixedBiome_0_20 = BiomeType.Forest;
    public BiomeType CurrentBiome
    {
        get => currentBiome;
        private set => currentBiome = value;
    }
    public event System.Action<BiomeType> OnBiomeChanged; // 바이옴 변경시 이벤트
    private int _biomeSegmentIndex = int.MinValue;
    private BiomeType _biomeSegmentValue = BiomeType.Forest;

    [Header("경험치 테이블")]
    [SerializeField] private float enemyExpFraction = 0.45f; // 적 경험치 계수
    [SerializeField] private float enemyGoldCoefficient = 77; // 적이 주는 골드 계수 (레벨에 곱해서 사용)
    [SerializeField] private double[] levelUpExpTable;       // 레벨업 필요 exp (공유)
    private double battleExpPool;                            // 이번 전투 누적 exp
    private double battleGoldPool;                           // 이번 전투 누적 gold

    public static RunManager Instance { get; private set; }
    public RunState currentRunState {get; private set;} //초기 상태
    public int currentLevel; // 현재 진행중인 라운드
    public int CurrentLevel => currentLevel;
    public int gold; // 이벤트나 상점에서 사용되는 재화
    // 이벤트에서 얻은 파티전체가 얻을 디버프
    [System.Serializable]
    public class PendingPartyDebuff
    {
        public enum Type { Stun, Poison, BurnAmp, MoveSlow, AttackSlow }
        public Type type;

        public float duration;
        public float dpsRatioOfMaxHp;
        public float multiplier;
    }

    private readonly List<PendingPartyDebuff> pendingPartyDebuffs = new();

    public List<GameObject> playerUnits = new List<GameObject>(); // 플레이어 캐릭터 리스트
    public List<GameObject> enemyUnits = new List<GameObject>();
    // 포메이션 저장용
    private Dictionary<int, Vector2Int> savedFormation = new Dictionary<int, Vector2Int>(); 
    
    public NodeType currentNodeType { get; private set; }
    public string currentEventId { get; private set; }  // 이벤트노드 id 저장

    public bool isInBattle; // 전투중인지 여부
    public bool isInEvent; // 이벤트 중인지 여부
    public bool isInReward; // 보상 선택 중인지 여부

    // 보상 리롤 변수
    private int rerollCountThisRound = 0;
    [SerializeField] private int rerollBaseCostPerRound = 200;
    [SerializeField] private int rerollCostStep = 2; // 리롤 가격 증가 배율

    private RewardDefinition pendingReward = null;
    private bool pendingWasFreeReward = false;
    private bool pendingIsShop;
    private int pendingShopCost;

    private int GatherHeroBuyCount = 0; // 용병초대권 구매횟수 저장용
    public bool usePriceTiers;
    public List<int> priceTiers;

    // 아이템 변수
    public int levelPotionBonus; // 경험의서 (경험비약의 효율을 1씩 올려줌)
    public int expAmulet;        // 경험부적 (경험치 획득 효율 증가 1개당 25%)
    public int goldAmulet;       // 부적금화 (골드 획득 효율증가 1개당 25%)

    // 다음 전투에만 적용되는 '적 레벨 오프셋'(라운드/보스 스케줄은 건드리지 않음)
    private int nextBattleEnemyLevelOffset = 0;
        
    // 싱글톤 (다른씬에 넘어갈일이 있으면 DontDestroyOnLoad 유지)
    void Awake()
    {        if (Instance == null)
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
        battleGoldPool = 0;
        isInBattle = false;
        isInEvent = false;
        isInReward = false;                
        currentRunState = RunState.OnMap; // 초기에 지도 부터 보여준다.
        currentBiome = fixedBiome_0_20;  // 숲에서 시작
        //튜토리얼 기능 추가시 작성

        // 기본 유닛 하나 추가 후
        GainUnit();
        // 시작 바이옴 지속 효과 적용(파티)
        ApplyBiomePersistentToParty(CurrentBiome);
        // 맵생성 함수 호출

        //맵열기 mapGenerator.ToggleMapView();
    }

    public void SelectNode(MapNode node) // Map에서 노드를 클릭할때 호출
    {
        currentNodeType = node.Type;
        currentLevel = node.Level;
        UpdateBiomeByRound(currentLevel); // 20라운드마다 바이옴 바꾸는 함수
        QuestManager.Instance?.OnRoundAdvanced(); // 퀘스트 진행시키는 함수
        currentEventId = "";

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
                mapGenerator.ToggleMapView();
                EnterEvent(node);
                // 이벤트 종류에 따라 분기 처리 필요
                break;
            case NodeType.Rest:
                EnterRest();
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
        enemySpawnManager.SpawnBattle(currentBiome, currentLevel, currentNodeType == NodeType.Boss, nextBattleEnemyLevelOffset);

        AllUnitsReady();
        // ===== ReadyState 진입 훅(전투 종료 후 다음 전투 전) =====
        TriggerEnterReadyHooks();


        //전투 시작 버튼 활성화 구현

        // 전투 경험치 초기화
        battleExpPool = 0;
        battleGoldPool = 0;
        EnsureLevelUpExpTable();

        // 보스전이면 대사 재생
        if (currentNodeType == NodeType.Boss)
        {
            PlayBossIntroDialogue();
        }
    }

    void EnterBattle()
    {
        // 대기상태에서 전투하기 버튼 선택하면
        isInBattle = true; 
        currentRunState = RunState.Battle;

        ResetBattleFlagsForAllUnits();

        if (currentNodeType == NodeType.Boss)
            enemySpawnManager.SpawnBossBattle(currentBiome, currentLevel, nextBattleEnemyLevelOffset);
        else
            enemySpawnManager.SpawnNormalBattle(currentBiome, currentLevel, nextBattleEnemyLevelOffset);

        // 다음 전투용 오프셋은 이 전투 스폰에 사용했으므로 초기화
        nextBattleEnemyLevelOffset = 0;
        
        bool hasPartyStun = HasPendingPartyStunAtBattleStart();

        if (hasPartyStun)
        {
            // 플레이어를 Idle로 풀기 전에 스턴/디버프를 먼저 적용해서
            // 이동 중 Move로 튀는 현상을 방지한다.
            ApplyPendingPartyDebuffs();
            EnemyUnitsIdle(); // 적만 전투 시작(아군은 스턴 상태로 유지)
        }
        else
        {
            AllUnitsIdle();
            ApplyPendingPartyDebuffs();
        }
    }
    
    public void StartBattle() // 전투 시작 버튼 누르면 호출
    {
        if (currentRunState != RunState.Ready)
            return;

        SavePlayerFormation();
        // 전투 시작 직전: FSM 타일 <-> Agent 타일 강제 동기화
        foreach (var go in playerUnits)
        {
            if (go == null || !go.activeInHierarchy) continue;
            var fsm = go.GetComponent<UnitFSM>();
            var agent = go.GetComponent<UnitGridAgent>();
            if (fsm != null && agent != null)
                agent.ForceSyncToTile(fsm.currentTilePosition);
            Debug.Log($"[P] id={fsm.unitId} active={go.activeInHierarchy} fsm={fsm.currentTilePosition} ag={agent.TilePos} " +
                $"occupiedBy={tileMapManager.IsOccupiedBy(agent.TilePos, fsm.unitId)}");
        }
        foreach (var go in enemyUnits)
        {
            if (go == null || !go.activeInHierarchy) continue;
            var fsm = go.GetComponent<UnitFSM>();
            var agent = go.GetComponent<UnitGridAgent>();
            if (fsm != null && agent != null)
                agent.ForceSyncToTile(fsm.currentTilePosition);
        }
        
        tileMapManager.RebuildOccupancyFromUnits(playerUnits, enemyUnits);
        isInBattle = true;
        currentRunState = RunState.Battle;

        ResetBattleFlagsForAllUnits();

        
        bool hasPartyStun = HasPendingPartyStunAtBattleStart();

        if (hasPartyStun)
        {
            ApplyPendingPartyDebuffs();
            EnemyUnitsIdle();
        }
        else
        {
            AllUnitsIdle();
            ApplyPendingPartyDebuffs();
        }
    
        // 바이옴 전투 시작 효과(모든 캐릭터) 적용
        ApplyBiomeBattleStartEffects();
    }

    public void BattleTest() // 나중에 삭제
    {
        enemySpawnManager.SpawnNormalBattle(currentBiome, currentLevel, nextBattleEnemyLevelOffset);
    }

    void EndBattle(bool isWin)
    {
        if (isWin)
        {
            AwardBattleExpToParty();
            RestorePlayerFormation();
            // 바이옴 전투 종료 효과(숲 회복 등)
            ApplyBiomeBattleEndEffects();
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
            double relicMul = 1.0 + 0.25 * goldAmulet;
            double basegold = enemyUnit.level * enemyGoldCoefficient + relicMul;
            gold += (int)basegold;
            // 돈 얻을때 시각효과 추가 고려중 (동전 올라오고 얼마 얻었는지 floatingmessage띄우기? )
        }

        // 리스트에서 제거
        enemyUnits.Remove(enemyGO);
        if (tileMapManager != null) tileMapManager.enemyUnits.Remove(enemyGO);

        // 타일 점유 해제는 네 기존 “죽음 처리”에서 하던 방식대로 유지
        Destroy(enemyGO);

        // 승리 체크
        CheckEndBattle();
    }

    // 전투 종료시 파티원에게 경험치 분배
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

    // 이벤트 노드 관련 함수
    void EnterEvent(MapNode node)
    {
        if (!node.IsResolved)
        {
            string id = EventManager.Instance.PickRandomEventId(); // 등급+가중치 로직 그대로 사용
            node.ResolveEventId(id);
        }

        currentEventId = node.EventId;

        Debug.Log($"[EnterEvent] node.EventId = '{node.EventId}'");
        EventManager.Instance.StartEvent(node.EventId);
    }
    
    // 도적단 조우
    public void StartEventBanditBattle(int presetKey)
    {
        currentRunState = RunState.Ready;
        isInBattle = false;

        enemyUnits.Clear();
        tileMapManager.enemyUnits.Clear();

        enemySpawnManager.SpawnBanditBattle(presetKey);
        AllUnitsReady();
        
        ToastManager.Instance?.Show("도적단이 습격했다!");
    }

    // 이벤트노드의 보상을 위해 id(키값) 넘겨줌
    public void EnterRewardFromEvent(string overrideEventId = null)
    {
        // 이벤트 결과에 따라 다른 보상풀을 쓰고 싶으면 overrideEventId로 덮어쓰기
        if (!string.IsNullOrEmpty(overrideEventId))
            currentEventId = overrideEventId;

        // Reward 상태로 진입
        isInReward = true;
        rerollCountThisRound = 0;
        currentRunState = RunState.Reward;

        GiveReward();
    }

    // 이벤트에서 지나가기 했을때 보상없이 상점만 이용
    public void EnterShopOnlyFromLeave()
    {
        // Reward 상태로 진입 (무료보상은 0개)
        isInReward = true;
        rerollCountThisRound = 0;
        currentRunState = RunState.Reward;

        GiveReward(forcedRewardCount: 0);
    }

    // 스턴디버프 
    public void AddPendingPartyStun(float duration)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.Stun,
            duration = duration
        });
    }
    // 중독 디버프
    public void AddPendingPartyPoison(float duration, float dpsRatioOfMaxHp)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.Poison,
            duration = duration,
            dpsRatioOfMaxHp = dpsRatioOfMaxHp
        });
    }

    // 다음 전투 적 레벨만 올리는 용도(라운드 값은 그대로 유지)
    public void AddNextBattleEnemyLevelOffset(int delta)
    {
        nextBattleEnemyLevelOffset += delta;
        if (nextBattleEnemyLevelOffset < 0) nextBattleEnemyLevelOffset = 0;
    }

    // 화상 증폭(ApplyBurnAmp) 예약
    public void AddPendingPartyBurnAmp(float duration, float multiplier)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.BurnAmp,
            duration = duration,
            multiplier = multiplier
        });
    }

    // 이동속도 감소 예약
    public void AddPendingPartyMoveSlow(float duration, float multiplier)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.MoveSlow,
            duration = duration,
            multiplier = multiplier
        });
    }

    // 공격속도(공격 딜레이) 감소 예약
    public void AddPendingPartyAttackSlow(float duration, float multiplier)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.AttackSlow,
            duration = duration,
            multiplier = multiplier
        });
    }

    


    //휴식노드 관련 함수
    public void EnterRest()
    {
        foreach (var unitGO in playerUnits)
        {
            if (unitGO == null) continue;
            var ufsm = unitGO.GetComponent<UnitFSM>();
            if (ufsm == null) continue;

            ufsm.ReviveToEmptyTile(false); // true : 반피회복 + 부활, false : 전체회복 + 부활
        }

        ToastManager.Instance?.Show("모두의 체력이 회복되었습니다!!!");
        mapGenerator.ToggleMapView();
        GoToNextRound();
    }

    void EnterReward()
    {
        isInReward = true;
        rerollCountThisRound = 0;
        currentRunState = RunState.Reward;
        // 보상 UI 구현
        // 선택후 다음라운드 진행 구현 
        // Map 다시 열기
        GiveReward();
    }

    private int GetRerollCost()
    {
        int round = (currentLevel == 0) ? 1 : currentLevel; 
        int baseCost = round * rerollBaseCostPerRound;
        int multiplier = 1 + rerollCountThisRound * rerollCostStep;
        return baseCost * multiplier;
    }

    private void OnSkipReward()
    {
        // 보상 선택 없이 종료
        pendingReward = null;
        FinishPending();

        isInReward = false;
        rewardPhasePanel.gameObject.SetActive(false);
        GoToNextRound();
    }

    void GiveReward(int forcedRewardCount = -1)
    {
        Debug.Log("보상실행");

        int rewardCount = (forcedRewardCount >= 0) ? forcedRewardCount : GetRewardCount();

        var rewardChoices = rewardManager.GetRewardChoices(
            currentLevel, rewardCount,
            currentNodeType, currentEventId,
            forceGlobalPool: false
        ) ?? new List<RewardDefinition>();

        var shopChoices = rewardManager.GetShopItems(currentLevel) ?? new List<RewardDefinition>();

        // 파티 최대 인원 도달했을 때 GainUnit 방지
        shopChoices.RemoveAll(r =>
            r != null
            && r.rewardType == RewardType.GainUnit
            && playerUnits.Count >= 10
        );

        rewardPhasePanel.Open(
            rewardChoices,
            shopChoices,
            OnRewardSelected,
            OnShopItemClicked,
            GetRerollCost,
            OnReroll,
            OnSkipReward   
        );

        rewardPhasePanel.gameObject.SetActive(true);
    }
    
    private int GetRewardCount()
    {   
        if (currentLevel >= 145) return 6;
        else if (currentLevel >= 95) return 5;
        else if (currentLevel >= 55) return 4;
        else if (currentLevel >= 25) return 3;
        else return 2; 
    }

    // 보상 선택시 보상을 저장 
    private void OnRewardSelected(RewardDefinition reward)
    {
        if (pendingReward != null) return; // 이미 선택중이면 무시
        pendingReward = reward;
        pendingIsShop = false;
        pendingWasFreeReward = true;
        pendingShopCost = 0;

        HandleRewardPick(reward);
    }

    // 상점은 코스트 비교 후 저장 
    private void OnShopItemClicked(RewardDefinition reward)
    {
        if (pendingReward != null) return;

        int cost = GetShopPrice(reward);
        if (gold < cost)
        {
            ToastManager.Instance?.Show("골드가 부족합니다.", 0.4f, 0.2f);
            return;
        }

        pendingReward = reward;
        pendingIsShop = true;
        pendingWasFreeReward = false;
        pendingShopCost = cost;

        HandleRewardPick(reward);
    }

    // 적용시점의 상점비용을 지불
    private void CommitPendingPurchaseIfNeeded()
    {
        if (!pendingIsShop) return;
        gold -= pendingShopCost;
    }

    // 보상, 상점용 변수 초기화
    private void FinishPending()
    {
        pendingReward = null;
        pendingIsShop = false;
        pendingShopCost = 0;
    }

    private void HandleRewardPick(RewardDefinition reward)
    {
        pendingReward = reward;

        if (reward.targetType == RewardTargetType.None)
        {
            CommitPendingPurchaseIfNeeded();
            ApplyRewardNoTarget(reward);

            // 파티원 추가 아이템 '구매'후 가격 올라감 보상은 안올라감
            AfterPurchaseSideEffectsIfNeeded(reward);

            FinishRewardFlow();
            return;
        }

        if (reward.targetType == RewardTargetType.RandomUnit)
        {
            CommitPendingPurchaseIfNeeded();   // ✅ 추가
            ApplyRewardToRandomUnit(reward);

            AfterPurchaseSideEffectsIfNeeded(reward);

            FinishRewardFlow();
            return;
        }
        
        OpenEquipToUnitUI(reward);
    }    
    
    private void FinishRewardFlow()
    {
        pendingReward = null;

        if (pendingWasFreeReward)
        {
            FinishPending();
            isInReward = false;
            rewardPhasePanel.gameObject.SetActive(false);
            GoToNextRound();
        }
        else
        {
            // 상점 아이템이면 계속 구매 가능
            FinishPending();
            rewardPhasePanel.gameObject.SetActive(true);
        }
    }

    // (보상선택후)라운드 끝 -> 다음라운드 시작전 까지 해야될 동작
    public void GoToNextRound()
    {
        // 다음 라운드로 넘어가는 준비

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

    // 파티모집권 구매할떄마다 Count늘려서 가격 상승
    private void AfterPurchaseSideEffectsIfNeeded(RewardDefinition reward)
    {
        if (pendingIsShop && reward.rewardType == RewardType.GainUnit)
            GatherHeroBuyCount++;
    }

    // 상점 구매비용 스케일링
    public int GetShopPrice(RewardDefinition r)
    {
        int round = currentLevel;

        float price = r.baseShopPrice;

        if (r.scaleWithRound)
        {
            // 배수 증가: base * (multiplier^(round-1))
            float mul = Mathf.Pow(r.roundPriceMultiplier, Mathf.Max(0, round - 1));
            price *= mul;
        }

        // 소환서만 구매 횟수 스케일(기존 선형 유지)
        if (r.scaleWithPurchaseCount)
            price += r.pricePerPurchase * GatherHeroBuyCount;

        return Mathf.Max(0, Mathf.RoundToInt(price));
    }

    // 골드 주는 아이템전용 스케일링 함수
    public int GetGoldAmount(RewardDefinition r)
    {
        int round = currentLevel;

        float goldAmount = r.goldAmount;

        if (r.scaleWithRound)
        {
            // 배수 증가: base * (multiplier^(round-1))
            float mul = Mathf.Pow(r.roundPriceMultiplier, Mathf.Max(0, round - 1));
            goldAmount *= mul;
        }

        float relicMul = 1f + 0.25f * goldAmulet;
        goldAmount *= relicMul;

        return Mathf.Max(0, Mathf.RoundToInt(goldAmount));
    }

    public int GetScaledGoldAmount(float baseGold, bool scaleWithRound, float roundMultiplier)
    {
        float goldAmount = baseGold;

        if (scaleWithRound)
        {
            int round = currentLevel;
            float mul = Mathf.Pow(roundMultiplier, Mathf.Max(0, round - 1));
            goldAmount *= mul;
        }

        float relicMul = 1f + 0.25f * goldAmulet;
        goldAmount *= relicMul;
        

        return Mathf.Max(0, Mathf.RoundToInt(goldAmount));
    }

    private void OnUnitSelected(UnitData selected)
    {
        SpawnUnit(selected);
        // 캐릭터 선택 후 맵 열기
        if(RunState.Reward == currentRunState)
        {
            return;
        }

        if(RunState.OnMap == currentRunState)
        {
            mapGenerator.ToggleMapView();
        }
       
    }

    //GainUnit과 EnemySpawn에서 사용
    public GameObject SpawnUnit(UnitData data)
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

        return unit;
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
                gold += GetGoldAmount(reward) ; // 이것도 스케일링 추가 해야함 유물계수 추가
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
                goldAmulet += reward.goldAmulet;
                break;
            
            case RewardType.Revive:
                foreach (var unitGO in playerUnits)
                {
                    var unitfsm = unitGO.GetComponent<UnitFSM>();
                    if (unitfsm == null) continue;

                    unitfsm.ReviveToEmptyTile(false);               
                }
                break;
            
            case RewardType.GainUnit:
                GainUnit();
                break;

            default:
                Debug.LogWarning($"RewardType {reward.rewardType} 는 타겟이 필요하거나 아직 미구현");
                break;
        }
    }
    
    //캐릭터 하나에게 적용하는 아이템
    private void OpenEquipToUnitUI(RewardDefinition reward)
    {
        var unitList = new List<Unit>();
        foreach (var unitGO in playerUnits)
        {
            var u = unitGO.GetComponent<Unit>();
            if (u != null) unitList.Add(u);
        }
        // 잠깐 비활성
        rewardPhasePanel.gameObject.SetActive(false);

        chooseUnitPanel.Open(
            unitList,
            (Unit selectedUnit) =>
            {
                CommitPendingPurchaseIfNeeded();
                ApplyRewardToUnit(reward, selectedUnit);

                AfterPurchaseSideEffectsIfNeeded(reward);

                FinishRewardFlow();
            },
            () =>
            {
                // 뒤로가기: 보상 선택으로 복귀 (아무 적용 안 함)
                pendingReward = null;
                FinishPending();
                rewardPhasePanel.gameObject.SetActive(true);
            }
        );
    }

    //랜덤으로 적용하는 아이템
    private void ApplyRewardToRandomUnit(RewardDefinition reward)
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

    // 캐릭터 한명에게 적용
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
            
            case RewardType.Revive:
                var unitfsm = unit.GetComponent<UnitFSM>();
                if(unitfsm != null) unitfsm.ReviveToEmptyTile(reward.reviveHerb);
                break;

            default:
                Debug.LogWarning($"RewardType {reward.rewardType} 는 Unit 대상 적용이 아직 구현되지 않음");
                break;
        }
    }

    private void OnReroll()
    {
        int cost = GetRerollCost();
        if (gold < cost)
        {
            ToastManager.Instance?.Show("골드가 부족합니다.", 0.5f, 0.2f);
            return;
        }

        gold -= cost;
        rerollCountThisRound++;

        int rewardCount = GetRewardCount();
        var rewardChoices = rewardManager.GetRewardChoices(
            currentLevel, rewardCount,
            currentNodeType, currentEventId,
            forceGlobalPool: true 
        );

        // shop은 그대로 유지
        var shopChoices = rewardManager.GetShopItems(currentLevel);

        rewardPhasePanel.Open(
            rewardChoices,
            shopChoices,
            OnRewardSelected,
            OnShopItemClicked,
            GetRerollCost,
            OnReroll,
            OnSkipReward
        );
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
    // 전투 노드에 진입할때 모든 유닛 준비상태로
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

    // 준비가 끝나면 전투시작 버튼 누르면 호출(전투 시작)
    
    // ReadyState(전투 전 대기) / 전투 종료 후 다음 전투 전 공통 훅
    void TriggerEnterReadyHooks()
    {
        // 아군
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var status = go.GetComponent<UnitStatusEffectController>();
            if (status != null) status.OnEnterReadyState();
        }

        // 적군(적도 스킬 있을 수 있으니)
        foreach (var go in enemyUnits)
        {
            if (go == null) continue;
            var status = go.GetComponent<UnitStatusEffectController>();
            if (status != null) status.OnEnterReadyState();
        }
    }

    void ResetBattleFlagsForAllUnits()
    {
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var status = go.GetComponent<UnitStatusEffectController>();
            if (status != null) status.ResetBattleFlags();
        }
        foreach (var go in enemyUnits)
        {
            if (go == null) continue;
            var status = go.GetComponent<UnitStatusEffectController>();
            if (status != null) status.ResetBattleFlags();
        }
    }

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

    void EnemyUnitsIdle()
    {
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
            // 게임 오버 처리
            // 점수판 최종점수 종합해서 게임매니저에 옮겨줌
        }
    }
    // 전투 종료 후 포메이션 복원
    void RestorePlayerFormation()
    {
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;
            if (!fsm.gameObject.activeInHierarchy) continue;
            //if (fsm.CurrentState == Faint) continue;
            
            if (savedFormation.TryGetValue(fsm.unitId, out var tile))
            {
                fsm.ForceReady(); // 다음 전투 준비 상태로
                fsm.SetPositionInstant(tile);                
            }
        }

        //tileMapManager.RebuildOccupancyFromUnits(playerUnits, enemyUnits);
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

    // 전체회복
    public void HealPartyFull()
    {
        foreach (var unitGO in playerUnits)
        {
            if (unitGO == null) continue;
            var u = unitGO.GetComponent<Unit>();
            if (u == null) continue;
            u.HealByPotion(0, 0, true);
        }
    }

    // 보스전 다이얼로그 함수
    void PlayBossIntroDialogue()
    {
        var line = enemySpawnManager.LastBossLine;
        var bossIndex = enemySpawnManager.LastBossIndex;

        if(bossIndex == 0)
        {
            var necroIndex = enemySpawnManager.LastNecromancerIndex;
            string dailid = $"NecromancerIntro_{necroIndex}";
            dialogueManager.StartById(dailid);
            return;
        }

        // 예시: 바이옴 + 라인으로 ID 규칙 잡기
        // Forest_A / Forest_B
        string id = $"BossIntro_{currentBiome}_{line}";

        // bossIndex 포함 예정
        //string id = $"BossIntro_{currentBiome}_{line}_{bossIndex}";

        dialogueManager.StartById(id);
    }

    // 바이옴 관련 함수 모음
    // ===== 바이옴 효과 적용용 유닛 리스트 헬퍼 =====
    private List<Unit> GetPartyUnitComponents()
    {
        var list = new List<Unit>(playerUnits.Count);
        foreach (var go in playerUnits)
        {
            if (go == null || !go.activeInHierarchy) continue;
            var u = go.GetComponent<Unit>();
            if (u != null) list.Add(u);
        }
        return list;
    }

    private List<Unit> GetEnemyUnitComponents()
    {
        var list = new List<Unit>(enemyUnits.Count);
        foreach (var go in enemyUnits)
        {
            if (go == null || !go.activeInHierarchy) continue;
            var u = go.GetComponent<Unit>();
            if (u != null) list.Add(u);
        }
        return list;
    }

void UpdateBiomeByRound(int round)
    {
        var newBiome = ResolveBiomeForRound(round);
        var oldBiome = CurrentBiome;

        if (newBiome != oldBiome)
        {
            // 지속형 바이옴 효과 원복/적용 (파티 유닛)
                        SwitchBiomePersistentEffects(oldBiome, newBiome);
CurrentBiome = newBiome;
            OnBiomeChanged?.Invoke(CurrentBiome);
        }
    }

    private BiomeType ResolveBiomeForRound(int round)
    {
        // 181~200: 미궁 고정
        if (round >= 181)
            return BiomeType.Labyrinth;

        // 0~20: 고정
        if (round <= 20)
            return fixedBiome_0_20;

        // 21~180: 20라운드 구간마다 랜덤(미궁 제외) - 구간 시작에 1번만 뽑고 유지
        // 21~40 -> 1, 41~60 -> 2 ... 161~180 -> 8
        int segment = (round - 1) / 20;

        if (segment != _biomeSegmentIndex)
        {
            _biomeSegmentIndex = segment;
            _biomeSegmentValue = PickRandomBiomeExcludingLabyrinth();
        }

        return _biomeSegmentValue;
    }
    
    // 미궁 제외한 바이옴 랜덤 선택
    private static BiomeType PickRandomBiomeExcludingLabyrinth()
    {
        BiomeType[] pool =
        {
            BiomeType.Forest,
            BiomeType.Plains,
            BiomeType.DeepForest,
            BiomeType.Cave,
            BiomeType.Lake,
            BiomeType.Snow,
            BiomeType.Desert
        };

        return pool[UnityEngine.Random.Range(0, pool.Length)];
    }   


    // ===== 전투 시작 예약 디버프 적용(이벤트 패널티 등) =====
    private void ApplyPendingPartyDebuffs()
    {
        if (pendingPartyDebuffs == null || pendingPartyDebuffs.Count == 0) return;

        foreach (var unitGO in playerUnits)
        {
            if (unitGO == null) continue;

            var fsm = unitGO.GetComponent<UnitFSM>();
            var status = unitGO.GetComponent<Component>(); // placeholder, use reflection on components

            foreach (var d in pendingPartyDebuffs)
            {
                if (d == null) continue;

                if (d.type == PendingPartyDebuff.Type.Stun)
                {
                    if (fsm != null) fsm.ApplyStun(d.duration);
                    continue;
                }

                // StatusEffectController는 프로젝트마다 메서드명이 달라서 리플렉션으로 안전 호출
                var sec = unitGO.GetComponent("UnitStatusEffectController");
                if (sec == null) continue;

                switch (d.type)
                {
                    case PendingPartyDebuff.Type.Poison:
                        // (duration, dpsRatioOrDps) 형태를 최대한 맞춤
                        InvokeAny(sec, new[] { "ApplyPoison" }, new object[] { d.duration, d.dpsRatioOfMaxHp });
                        break;

                    case PendingPartyDebuff.Type.BurnAmp:
                        InvokeAny(sec, new[] { "ApplyBurnAmp" }, new object[] { d.multiplier, d.duration });
                        InvokeAny(sec, new[] { "ApplyBurnAmp" }, new object[] { d.duration, d.multiplier });
                        break;

                    case PendingPartyDebuff.Type.MoveSlow:
                        InvokeAny(sec, new[] { "ApplyMoveSlow", "ApplyMoveSpeedMultiplier", "ApplyMoveSpeedMul" }, new object[] { d.multiplier, d.duration });
                        InvokeAny(sec, new[] { "ApplyMoveSlow", "ApplyMoveSpeedMultiplier", "ApplyMoveSpeedMul" }, new object[] { d.duration, d.multiplier });
                        break;

                    case PendingPartyDebuff.Type.AttackSlow:
                        InvokeAny(sec, new[] { "ApplyAttackSlow", "ApplyAttackDelayMultiplier", "ApplyAttackDelayMul" }, new object[] { d.multiplier, d.duration });
                        InvokeAny(sec, new[] { "ApplyAttackSlow", "ApplyAttackDelayMultiplier", "ApplyAttackDelayMul" }, new object[] { d.duration, d.multiplier });
                        break;
                }
            }
        }

        pendingPartyDebuffs.Clear();
    }

    // 온천 등으로 '다음 전투 시작 시 파티 스턴'이 예약되어 있는지 확인
    private bool HasPendingPartyStunAtBattleStart()
    {
        if (pendingPartyDebuffs == null) return false;
        for (int i = 0; i < pendingPartyDebuffs.Count; i++)
        {
            var d = pendingPartyDebuffs[i];
            if (d != null && d.type == PendingPartyDebuff.Type.Stun && d.duration > 0f)
                return true;
        }
        return false;
    }

    private static void InvokeAny(object target, string[] methodNames, object[] args)
    {
        var t = target.GetType();
        foreach (var name in methodNames)
        {
            var mi = t.GetMethod(name);
            if (mi == null) continue;
            var ps = mi.GetParameters();
            if (ps.Length != args.Length) continue;
            try { mi.Invoke(target, args); return; } catch { }
        }
    }

}
