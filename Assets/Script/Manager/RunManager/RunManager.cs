using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BossCategory = EnemySpawnManager.BossCategory;

public enum RunState
{
    OnMap,
    Ready,
    Battle,
    Reward,
    Event,
}


public partial class RunManager : MonoBehaviour
{
    [Header("유닛 풀")]
    [SerializeField] private List<UnitData> playerUnitPool;
    [SerializeField] private List<UnitData> enemyUnitPool;
    [SerializeField] private UnitSelectPanel unitSelectPanel;
    [SerializeField] private ChooseUnitPanel chooseUnitPanel;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private TileMapManager tileMapManager;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private RewardPhasePanel rewardPhasePanel;
    [SerializeField] private EnemySpawnManager enemySpawnManager;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private BiomeType currentBiome;

    [Header("오디오")]
    [SerializeField] private float bgmFadeSeconds = 0.8f;
    [SerializeField] private BgmId eventBgmId = BgmId.Event_Generic;
    [SerializeField] private BgmId bossDialogueBgmId = BgmId.Event_BossIntro;
    [SerializeField] private BgmId bossBattleBgmId = BgmId.BossFight_Generic;
    [SerializeField] private BgmId necroDialogueBgmId = BgmId.Event_NecromancerIntro;
    [SerializeField] private BgmId necroBattleBgmId = BgmId.BossFight_Necromancer;
    [SerializeField] private BgmId GameClearId = BgmId.Game_Clear;
    [SerializeField] private BgmId GameOverId = BgmId.Game_Over;
    
    private BgmId? lastPlayedBgmId;

    [Header("바이옴")]
    [SerializeField] private BiomeType fixedBiome_0_20 = BiomeType.Forest;
    public BiomeType CurrentBiome
    {
        get => currentBiome;
        private set => currentBiome = value;
    }
    public event System.Action<BiomeType> OnBiomeChanged;
    private int _biomeSegmentIndex = int.MinValue;
    private BiomeType _biomeSegmentValue = BiomeType.Forest;

    [Header("경험치/골드")]
    [SerializeField] private float enemyExpFraction = 0.45f;
    [SerializeField] private float enemyGoldCoefficient = 77;
    [SerializeField] private int baseStartGold = 1000;
    [SerializeField] private double[] levelUpExpTable;
    private double battleExpPool;
    private double battleGoldPool;

    [System.Serializable]
    private class AwakenPair
    {
        public UnitData baseForm;      // 기본 유닛데이터
        public UnitData awakenedForm;  // 각성 유닛데이터
    }

    [Header("각성 매핑")]
    [SerializeField] private List<AwakenPair> awakenPairs = new();

    public static RunManager Instance { get; private set; }
    public RunState currentRunState {get; private set;}
    public int currentLevel;
    public int CurrentLevel => currentLevel;
    public int gold;

    [Header("결과/통계")]
    [SerializeField] private GameResultPanel gameResultPanel;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string titleSceneName = "StartScene";
    [SerializeField] private float scoreRoundWeight = 100f;
    [SerializeField] private float scoreGoldWeight = 0.01f;
    [SerializeField] private float scoreKillWeight = 20f;
    [SerializeField] private float scorePowerWeight = 1f;
    private int totalEnemyKills;
    private bool isRunTerminated;

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

    public List<GameObject> playerUnits = new List<GameObject>();
    public List<GameObject> enemyUnits = new List<GameObject>();

    private Dictionary<int, Vector2Int> savedFormation = new Dictionary<int, Vector2Int>(); 
    
    public NodeType currentNodeType { get; private set; }
    public string currentEventId { get; private set; }

    public bool isInBattle;
    public bool isInEvent;
    public bool isInReward;

    [Header("준비 UI 참조")]
    [SerializeField] private Button fleeButton;

    [Header("전투 회피 설정")]
    [Range(0f, 1f)] [SerializeField] private float fleeSuccessRate = 0.35f;


    private int rerollCountThisRound = 0;
    [SerializeField] private int rerollBaseCostPerRound = 200;
    [SerializeField] private int rerollCostStep = 2;

    private RewardDefinition pendingReward = null;
    private bool pendingWasFreeReward = false;
    private bool pendingIsShop;
    private int pendingShopCost;

    private int GatherHeroBuyCount = 0;
    public bool usePriceTiers;
    public List<int> priceTiers;


    public int levelPotionBonus;
    public int expAmulet;
    public int goldAmulet;


    private int nextBattleEnemyLevelOffset = 0;
        

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameResultPanel?.Configure(RetryRun, GoToTitleScene);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        MarkPlayerUnits();
        if (SaveManager.instance != null && SaveManager.instance.HasPendingRunLoad && SaveManager.instance.saveData != null)
        {
            RestoreRun(SaveManager.instance.saveData);
        }
        else
        {
            StartNewRun();
        }
    }

    void StartNewRun()
    {
        Time.timeScale = 1f;
        isRunTerminated = false;
        totalEnemyKills = 0;
        currentLevel = 0;
        gold = CalculateStartGold();
        playerUnits.Clear();
        EnsureLevelUpExpTable();
        battleExpPool = 0;
        battleGoldPool = 0;
        isInBattle = false;
        isInEvent = false;
        isInReward = false;                
        currentRunState = RunState.OnMap;
        currentBiome = fixedBiome_0_20;
        
        GainUnit();

        ApplyBiomePersistentToParty(CurrentBiome);

    }

    private void MarkPlayerUnits()
    {
        if (playerUnitPool == null) return;
        for (int i = 0; i < playerUnitPool.Count; i++)
        {
            if (playerUnitPool[i] != null)
                playerUnitPool[i].isPlayerUnit = true;
        }
    }

    private int CalculateStartGold()
    {
        int bonus = MetaProgressManager.Instance != null ? MetaProgressManager.Instance.GetStartGoldBonus() : 0;
        int total = baseStartGold + bonus;
        return Mathf.Max(0, total);
    }

    private bool IsFinalNecromancerRound()
    {
        // 네크로맨서 라운드 중 최종(200라운드)만 전용 BGM 사용
        return enemySpawnManager != null &&
               enemySpawnManager.IsNecromancerRound(currentLevel) &&
               currentLevel >= 200;
    }

    private BgmId GetBiomeBgmId(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Forest => BgmId.Biome_Forest,
            BiomeType.Plains => BgmId.Biome_Plains,
            BiomeType.DeepForest => BgmId.Biome_DeepForest,
            BiomeType.Cave => BgmId.Biome_Cave,
            BiomeType.Lake => BgmId.Biome_Lake,
            BiomeType.Snow => BgmId.Biome_Snow,
            BiomeType.Desert => BgmId.Biome_Desert,
            BiomeType.Labyrinth => BgmId.Biome_Labyrinth,
            _ => BgmId.Biome_Forest
        };
    }

    private void PlayBiomeBgm(bool force = false)
    {
        var bgm = GetBiomeBgmId(CurrentBiome);
        if (!force && lastPlayedBgmId.HasValue && lastPlayedBgmId.Value.Equals(bgm))
            return;

        AudioManager.Instance?.PlayBgm(bgm, bgmFadeSeconds);
        lastPlayedBgmId = bgm;
    }

    private void PlayEventBgm()
    {
        AudioManager.Instance?.PlayBgm(eventBgmId, bgmFadeSeconds);
        lastPlayedBgmId = eventBgmId;
    }

    private void PlayBossDialogueBgm()
    {
        var bgm = IsFinalNecromancerRound() ? necroDialogueBgmId : bossDialogueBgmId;
        AudioManager.Instance?.PlayBgm(bgm, bgmFadeSeconds);
        lastPlayedBgmId = bgm;
    }

    private void PlayBossBattleBgm()
    {
        var bgm = IsFinalNecromancerRound() ? necroBattleBgmId : bossBattleBgmId;
        AudioManager.Instance?.PlayBgm(bgm, bgmFadeSeconds);
        lastPlayedBgmId = bgm;
    }

    public void SelectNode(MapNode node)
    {
        currentNodeType = node.Type;
        var prevBiome = CurrentBiome;
        currentLevel = node.Level;
        int effectiveRound = Mathf.Max(1, currentLevel); // 0라운드는 1라운드 스케일로 취급
        UpdateBiomeByRound(effectiveRound);
        bool biomeChanged = CurrentBiome != prevBiome;
        bool isFirstNode = currentLevel == 0;
        QuestManager.Instance?.OnRoundAdvanced();
        currentEventId = "";


        switch (currentNodeType)
        {
            case NodeType.Combat:
                PlayBiomeBgm(biomeChanged || isFirstNode);
                mapGenerator.MapViewOff();
                EnterReady();                
                break;
            case NodeType.Boss:
                mapGenerator.MapViewOff();
                EnterReady();

                break;
            case NodeType.Event:
                PlayEventBgm();
                mapGenerator.MapViewOff();
                EnterEvent(node);

                break;
            case NodeType.Rest:
                PlayBiomeBgm(biomeChanged || isFirstNode);
                EnterRest();

                break;
            default:
                break;
        }
    }



    void EnterReady()
    {
        currentRunState = RunState.Ready;
        isInBattle = false;
        
        enemyUnits.Clear();
        tileMapManager.enemyUnits.Clear();

        int spawnRound = Mathf.Max(1, currentLevel); // 0라운드도 1라운드 스케일로 스폰
        enemySpawnManager.SpawnBattle(currentBiome, spawnRound, currentNodeType == NodeType.Boss, nextBattleEnemyLevelOffset);

        nextBattleEnemyLevelOffset = 0;
        AllUnitsReady();

        TriggerEnterReadyHooks();
        RefreshFleeButton();

        battleExpPool = 0;
        battleGoldPool = 0;
        EnsureLevelUpExpTable();


        if (currentNodeType == NodeType.Boss)
        {
            PlayBossDialogueBgm();
            PlayBossIntroDialogue();
        }
    }

    void EnterBattle()
    {

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
    }
    
    public void TryFleeFromReady(Button fleeButton = null)
    {
        if (currentRunState != RunState.Ready)
            return;

        // 일반 전투 노드에서만 사용
        if (currentNodeType != NodeType.Combat)
        {
            ToastManager.Instance?.Show("도망 칠 수 없는 전투입니다.", 0.4f, 0.2f);
            return;
        }

        var button = fleeButton != null ? fleeButton : this.fleeButton;

        if (Random.value <= fleeSuccessRate)
        {
            ToastManager.Instance?.Show("도망 성공! 보상 없이 이동합니다.", 0.6f, 0.2f);
            if (button != null) button.interactable = false;
            DespawnCurrentEnemies();
            EnterShopOnlyFromLeave();
        }
        else
        {
            ToastManager.Instance?.Show("도망 실패!", 0.6f, 0.2f);
            if (button != null)
                button.gameObject.SetActive(false);
        }
    }

    void DespawnCurrentEnemies()
    {
        for (int i = 0; i < enemyUnits.Count; i++)
        {
            var go = enemyUnits[i];
            if (go == null) continue;

            var fsm = go.GetComponent<UnitFSM>();
            if (fsm != null && tileMapManager != null)
                tileMapManager.ReleaseUnitAll(fsm.unitId);

            Destroy(go);
        }

        enemyUnits.Clear();
        if (tileMapManager != null)
        {
            tileMapManager.enemyUnits.Clear();
            tileMapManager.RebuildOccupancyFromUnits(playerUnits, enemyUnits);
        }
    }

    void RefreshFleeButton()
    {
        if (fleeButton == null) return;

        bool show = currentRunState == RunState.Ready && currentNodeType == NodeType.Combat;
        fleeButton.gameObject.SetActive(show);
        if (show) fleeButton.interactable = true;
    }
    
    public void StartBattle()
    {
        if (currentRunState != RunState.Ready)
            return;

        if (currentNodeType == NodeType.Boss)
        {
            PlayBossBattleBgm();
        }

        SavePlayerFormation();

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
        RefreshFleeButton();

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
    

        ApplyBiomeBattleStartEffects();
    }

    void EndBattle(bool isWin)
    {
        if (isWin)
        {
            AwardBattleExpToParty();
            RestorePlayerFormation();

            ApplyBiomeBattleEndEffects();
            EnterReward();
        }
        else
        {
            ShowGameOverUI();
        }
    }

    public void OnEnemyDefeated(GameObject enemyGO)
    {
        if (isRunTerminated) return;
        if (enemyGO == null) return;

        var enemyUnit = enemyGO.GetComponent<Unit>();
        if (enemyUnit != null)
        {

            double baseReward = GetRequiredExp(enemyUnit.level) * enemyExpFraction;
            battleExpPool += baseReward;
            double relicMul = 1.0 + 0.25 * goldAmulet;
            double basegold = enemyUnit.level * enemyGoldCoefficient + relicMul;
            gold += (int)basegold;

        }


        enemyUnits.Remove(enemyGO);
        if (tileMapManager != null) tileMapManager.enemyUnits.Remove(enemyGO);

        totalEnemyKills++;

        Destroy(enemyGO);


        CheckEndBattle();
    }


    private void AwardBattleExpToParty()
    {
        if (battleExpPool <= 0) return;


        var receivers = new List<Unit>();
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            if (u.hp <= 0) continue;
            receivers.Add(u);
        }

        if (receivers.Count == 0) return;

        double per = battleExpPool / receivers.Count;


        double relicMul = 1.0 + 0.25 * expAmulet;

        foreach (var u in receivers)
        {
            u.GainExp(per * relicMul);
        }

        battleExpPool = 0;
    }


    void EnterEvent(MapNode node)
    {
        if (!node.IsResolved)
        {
            string id = EventManager.Instance.PickRandomEventId();
            node.ResolveEventId(id);
        }

        currentEventId = node.EventId;

        Debug.Log($"[EnterEvent] node.EventId = '{node.EventId}'");
        EventManager.Instance.StartEvent(node.EventId);
    }
    

    public void StartEventBanditBattle(int presetKey)
    {
        currentRunState = RunState.Ready;
        isInBattle = false;

        enemyUnits.Clear();
        tileMapManager.enemyUnits.Clear();

        enemySpawnManager.SpawnBanditBattle(presetKey);
        AllUnitsReady();
        
        ToastManager.Instance?.Show("도적 전투 발생!");
    }


    public void EnterRewardFromEvent(string overrideEventId = null)
    {

        if (!string.IsNullOrEmpty(overrideEventId))
            currentEventId = overrideEventId;


        isInReward = true;
        rerollCountThisRound = 0;
        currentRunState = RunState.Reward;

        GiveReward();
    }


    public void EnterShopOnlyFromLeave()
    {

        isInReward = true;
        rerollCountThisRound = 0;
        currentRunState = RunState.Reward;

        GiveReward(forcedRewardCount: 0);
    }


    public void AddPendingPartyStun(float duration)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.Stun,
            duration = duration
        });
    }

    public void AddPendingPartyPoison(float duration, float dpsRatioOfMaxHp)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.Poison,
            duration = duration,
            dpsRatioOfMaxHp = dpsRatioOfMaxHp
        });
    }


    public void AddNextBattleEnemyLevelOffset(int delta)
    {
        nextBattleEnemyLevelOffset += delta;
        if (nextBattleEnemyLevelOffset < 0) nextBattleEnemyLevelOffset = 0;
    }


    public void AddPendingPartyBurnAmp(float duration, float multiplier)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.BurnAmp,
            duration = duration,
            multiplier = multiplier
        });
    }


    public void AddPendingPartyMoveSlow(float duration, float multiplier)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.MoveSlow,
            duration = duration,
            multiplier = multiplier
        });
    }


    public void AddPendingPartyAttackSlow(float duration, float multiplier)
    {
        pendingPartyDebuffs.Add(new PendingPartyDebuff
        {
            type = PendingPartyDebuff.Type.AttackSlow,
            duration = duration,
            multiplier = multiplier
        });
    }

    



    public void EnterRest()
    {
        foreach (var unitGO in playerUnits)
        {
            if (unitGO == null) continue;
            var ufsm = unitGO.GetComponent<UnitFSM>();
            if (ufsm == null) continue;

            ufsm.ReviveToEmptyTile(false);
        }

        ToastManager.Instance?.Show("모든 아군이 회복되었습니다!", 0.4f, 0.2f);
        mapGenerator.MapViewOn();
        GoToNextRound();
    }

    void EnterReward()
    {
        isInReward = true;
        rerollCountThisRound = 0;
        currentRunState = RunState.Reward;



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

        pendingReward = null;
        FinishPending();

        isInReward = false;
        rewardPhasePanel.gameObject.SetActive(false);
        GoToNextRound();
    }

    void GiveReward(int forcedRewardCount = -1)
    {
        Debug.Log("Reward 시작");

        int rewardCount = (forcedRewardCount >= 0) ? forcedRewardCount : GetRewardCount();

        var rewardChoices = rewardManager.GetRewardChoices(
            currentLevel, rewardCount,
            currentNodeType, currentEventId,
            forceGlobalPool: false
        ) ?? new List<RewardDefinition>();

        var shopChoices = rewardManager.GetShopItems(currentLevel) ?? new List<RewardDefinition>();


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


    private void OnRewardSelected(RewardDefinition reward)
    {
        if (pendingReward != null) return;
        pendingReward = reward;
        pendingIsShop = false;
        pendingWasFreeReward = true;
        pendingShopCost = 0;

        HandleRewardPick(reward);
    }


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


    private void CommitPendingPurchaseIfNeeded()
    {
        if (!pendingIsShop) return;
        gold -= pendingShopCost;
    }


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


            AfterPurchaseSideEffectsIfNeeded(reward);

            FinishRewardFlow();
            return;
        }

        if (reward.targetType == RewardTargetType.RandomUnit)
        {
            CommitPendingPurchaseIfNeeded();
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

            FinishPending();
            rewardPhasePanel.gameObject.SetActive(true);
        }
    }


    public void GoToNextRound()
    {
        mapGenerator.MapViewOn();
        currentRunState = RunState.OnMap;

        // 자동 저장
        var snapshot = BuildSaveData(mapGenerator);
        SaveManager.instance?.SaveGame(snapshot);
    }

    private SaveData BuildSaveData(MapGenerator mapGen)
    {
        var data = new SaveData();
        data.currentLevel = currentLevel;
        data.gold = gold;
        data.currentBiome = CurrentBiome;
        data.rerollCountThisRound = rerollCountThisRound;
        data.totalEnemyKills = totalEnemyKills;
        data.nextBattleEnemyLevelOffset = nextBattleEnemyLevelOffset;
        data.levelPotionBonus = levelPotionBonus;
        data.expAmulet = expAmulet;
        data.goldAmulet = goldAmulet;

        // 보류 중인 디버프 저장
        data.pendingDebuffs.Clear();
        foreach (var debuff in pendingPartyDebuffs)
        {
            data.pendingDebuffs.Add(new PendingDebuffState
            {
                type = debuff.type,
                duration = debuff.duration,
                dpsRatioOfMaxHp = debuff.dpsRatioOfMaxHp,
                multiplier = debuff.multiplier
            });
        }

        // 파티 정보 저장
        data.playerUnits.Clear();
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            var unit = go.GetComponent<Unit>();
            if (fsm == null || unit == null) continue;

            data.playerUnits.Add(new PlayerUnitState
            {
                unitDataName = !string.IsNullOrEmpty(unit.originUnitDataName) ? unit.originUnitDataName : unit.unitName,
                level = unit.level,
                exp = unit.exp,
                hp = unit.hp,
                mp = unit.mp,
                tileX = fsm.currentTilePosition.x,
                tileY = fsm.currentTilePosition.y,
                isAlive = unit.hp > 0
            });

            // 장비 저장 (아이템 이름 우선, 없으면 에셋 이름)
            var savedUnit = data.playerUnits[^1];
            foreach (var eq in unit.equippedItems)
            {
                if (eq == null) continue;
                string name = !string.IsNullOrEmpty(eq.itemName) ? eq.itemName : eq.name;
                savedUnit.equippedItemNames.Add(name);
            }
        }

        // 맵 상태 저장
        if (mapGen != null)
        {
            mapGen.EnsureCurrentNode(currentLevel);
            Debug.Log("[RunManager] BuildSaveData -> FillSaveData(Map)");
            mapGen.FillSaveData(data);
        }

        data.isValid = true;
        return data;
    }

    private void RestoreRun(SaveData data)
    {
        if (data == null || !data.isValid)
        {
            StartNewRun();
            return;
        }

        Time.timeScale = 1f;
        isRunTerminated = false;
        isInBattle = false;
        isInEvent = false;
        isInReward = false;

        totalEnemyKills = data.totalEnemyKills;
        currentLevel = data.currentLevel;
        gold = data.gold;
        currentBiome = data.currentBiome;
        nextBattleEnemyLevelOffset = data.nextBattleEnemyLevelOffset;
        levelPotionBonus = data.levelPotionBonus;
        expAmulet = data.expAmulet;
        goldAmulet = data.goldAmulet;

        EnsureLevelUpExpTable();
        battleExpPool = 0;
        battleGoldPool = 0;
        currentRunState = RunState.OnMap;

        pendingPartyDebuffs.Clear();
        foreach (var debuff in data.pendingDebuffs)
        {
            pendingPartyDebuffs.Add(new PendingPartyDebuff
            {
                type = debuff.type,
                duration = debuff.duration,
                dpsRatioOfMaxHp = debuff.dpsRatioOfMaxHp,
                multiplier = debuff.multiplier
            });
        }

        foreach (var go in enemyUnits)
        {
            if (go != null) Destroy(go);
        }
        enemyUnits.Clear();
        tileMapManager.enemyUnits.Clear();

        // 파티 복원
        foreach (var go in playerUnits)
        {
            if (go != null) Destroy(go);
        }
        playerUnits.Clear();
        tileMapManager.playerUnits.Clear();

        foreach (var pu in data.playerUnits)
        {
            if (string.IsNullOrEmpty(pu.unitDataName)) continue;

            // 기본 UnitData 찾기
            var baseData = playerUnitPool.Find(u => u != null && (u.name == pu.unitDataName || u.unitName == pu.unitDataName));
            if (baseData == null) continue;

            // 레벨이 100 이상이면 즉시 각성 데이터로 교체
            UnitData spawnData = pu.level >= 100 ? ResolveAwakenedData(baseData) : baseData;
            spawnData.level = Mathf.Max(1, pu.level);
            spawnData.isPlayerUnit = true;

            var unitGO = SpawnUnitAtTile(spawnData, new Vector2Int(pu.tileX, pu.tileY));
            var unitComp = unitGO.GetComponent<Unit>();
            if (unitComp == null) continue;

            unitComp.level = spawnData.level;
            unitComp.exp = pu.exp;
            unitComp.RefreshStats(); // 레벨 반영
            unitComp.hp = System.Math.Max(0, System.Math.Min(pu.hp, unitComp.maxHp));
            unitComp.mp = Mathf.Clamp(pu.mp, 0, unitComp.maxMp);
            if (!pu.isAlive)
            {
                unitComp.hp = 0;
            }

            // 장비 복원
            if (pu.equippedItemNames != null)
            {
                foreach (var eqName in pu.equippedItemNames)
                {
                    var eq = ResolveEquipmentByName(eqName);
                    if (eq != null)
                    {
                        unitComp.Equip(eq);
                    }
                }
            }
        }

        tileMapManager.RebuildOccupancyFromUnits(playerUnits, enemyUnits);

        ApplyBiomePersistentToParty(CurrentBiome);

        // 맵 복원은 MapGenerator가 Start 시점에 처리함
        mapGenerator?.MapViewOn();

        if (SaveManager.instance != null)
        {
            SaveManager.instance.loadRequested = false;
            SaveManager.pendingAutoLoad = false;
        }
    }

    // 설정창 등에서 호출: 현재 진행 상태를 저장하고 타이틀로 복귀
    public void SaveAndReturnToTitle()
    {
        // 요청에 따라 타이틀 이동 시 추가 저장을 하지 않음 (이미 라운드 종료 시 자동 저장)
        Debug.Log("[RunManager] SaveAndReturnToTitle: skip save, just go title");
        Time.timeScale = 1f;
        GoToTitleScene();
    }

    private Equipment ResolveEquipmentByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string target = name.Trim();

        // 1) 적 장비 풀에서 탐색
        if (enemySpawnManager != null && enemySpawnManager.enemyEquipmentPools != null)
        {
            foreach (var pool in enemySpawnManager.enemyEquipmentPools)
            {
                if (pool == null || pool.equipments == null) continue;
                foreach (var eq in pool.equipments)
                {
                    if (eq == null) continue;
                    if (eq.itemName == target || eq.name == target)
                        return eq;
                }
            }
        }

        // 2) 유닛 기본 장비에서 탐색
        foreach (var ud in playerUnitPool)
        {
            if (ud == null || ud.startingEquipments == null) continue;
            foreach (var eq in ud.startingEquipments)
            {
                if (eq == null) continue;
                if (eq.itemName == target || eq.name == target)
                    return eq;
            }
        }

        // 3) 로드된 모든 Equipment에서 탐색
        var all = Resources.FindObjectsOfTypeAll<Equipment>();
        foreach (var eq in all)
        {
            if (eq == null) continue;
            if (eq.itemName == target || eq.name == target)
                return eq;
        }

        Debug.LogWarning($"[SaveLoad] Equipment '{name}'을(를) 찾지 못했습니다.");
        return null;
    }


    public void GainUnit()
    {
        List<UnitData> candidates = GetRandomUnits(3);
        unitSelectPanel.Open(candidates, OnUnitSelected);
    }

    private List<UnitData> GetRandomUnits(int count)
    {
        var list = new List<UnitData>(playerUnitPool);


        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }

        if (list.Count > count)
            list.RemoveRange(count, list.Count - count);

        return list;
    }


    private void AfterPurchaseSideEffectsIfNeeded(RewardDefinition reward)
    {
        if (pendingIsShop && reward.rewardType == RewardType.GainUnit)
            GatherHeroBuyCount++;
            rewardPhasePanel?.RefreshShopPrices();
    }


    public int GetShopPrice(RewardDefinition r)
    {
        int round = currentLevel;

        float price = r.baseShopPrice;

        if (r.scaleWithRound)
        {

            float mul = Mathf.Pow(r.roundPriceMultiplier, Mathf.Max(0, round - 1));
            price *= mul;
        }


        if (r.scaleWithPurchaseCount)
            price += r.pricePerPurchase * GatherHeroBuyCount;

        return Mathf.Max(0, Mathf.RoundToInt(price));
    }


    public int GetGoldAmount(RewardDefinition r)
    {
        int round = currentLevel;

        float goldAmount = r.goldAmount;

        if (r.scaleWithRound)
        {

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

        if(RunState.Reward == currentRunState)
        {
            return;
        }

        if(RunState.OnMap == currentRunState)
        {
            mapGenerator.MapViewOn();
        }
       
    }


    public GameObject SpawnUnit(UnitData data)
    {
        if (data != null)
            data.isPlayerUnit = true;

        GameObject unit = Instantiate(data.prefab);


        Vector2Int startTile;
        tileMapManager.GetEmptyTile(out startTile);


        UnitFSM fsm = unit.GetComponent<UnitFSM>();
        fsm.Initialize(tileMapManager, startTile);

        Unit unitComp = unit.GetComponent<Unit>();
        if (unitComp != null)
        {

            unitComp.ApplyData(data);
        }


        playerUnits.Add(unit);
        tileMapManager.playerUnits.Add(unit);

        return unit;
    }

    // baseData를 복제한 뒤 각성 템플릿(스킬/프리팹/이름 등)만 덮어쓴 최종 데이터 반환
    private UnitData ResolveAwakenedData(UnitData baseData)
    {
        var pair = awakenPairs.Find(p => p.baseForm == baseData);
        if (pair == null || pair.awakenedForm == null)
            return baseData;

        var clone = ScriptableObject.Instantiate(baseData);
        var template = pair.awakenedForm;
        clone.isPlayerUnit = baseData.isPlayerUnit;

        if (!string.IsNullOrEmpty(template.unitName)) clone.unitName = template.unitName;
        if (template.portrait != null) clone.portrait = template.portrait;
        if (template.uiPortrait != null) clone.uiPortrait = template.uiPortrait;
        if (!string.IsNullOrEmpty(template.unitSummary)) clone.unitSummary = template.unitSummary;
        if (template.prefab != null) clone.prefab = template.prefab;

        if (template.fullManaSkill != null) clone.fullManaSkill = template.fullManaSkill;
        if (!string.IsNullOrEmpty(template.fullManaSkillDescription))
            clone.fullManaSkillDescription = template.fullManaSkillDescription;

        if (template.startingPassives != null && template.startingPassives.Count > 0)
            clone.startingPassives = new List<SkillDefinition>(template.startingPassives);

        if (template.startingEquipments != null && template.startingEquipments.Count > 0)
            clone.startingEquipments = new List<Equipment>(template.startingEquipments);

        return clone;
    }

    // 유닛 레벨이 99 이상이면 각성폼으로 교체 시도
    public void TryAwaken(Unit unit)
    {
        if (unit == null || unit.level < 99) return;

        var pair = awakenPairs.Find(p => p.baseForm != null && p.baseForm.unitName == unit.unitName);
        if (pair == null || pair.awakenedForm == null) return; // 매핑 없으면 패스

        var fsmOld = unit.GetComponent<UnitFSM>();
        if (fsmOld == null) return;

        Vector2Int tile = fsmOld.currentTilePosition;

        // 기존 유닛 정리
        tileMapManager.ReleaseUnitAll(fsmOld.unitId);
        playerUnits.Remove(unit.gameObject);
        tileMapManager.playerUnits.Remove(unit.gameObject);
        Destroy(unit.gameObject);

        // 각성 데이터 준비
        var awakenedData = ResolveAwakenedData(pair.baseForm);
        awakenedData.level = unit.level; // 레벨 유지
        awakenedData.isPlayerUnit = true;

        // 동일 타일에 스폰
        SpawnUnitAtTile(awakenedData, tile);
    }

    private GameObject SpawnUnitAtTile(UnitData data, Vector2Int tile)
    {
        if (data != null)
            data.isPlayerUnit = true;

        GameObject unit = Instantiate(data.prefab);

        UnitFSM fsm = unit.GetComponent<UnitFSM>();
        fsm.Initialize(tileMapManager, tile);

        Unit unitComp = unit.GetComponent<Unit>();
        if (unitComp != null)
        {
            unitComp.ApplyData(data);
        }

        playerUnits.Add(unit);
        tileMapManager.playerUnits.Add(unit);
        return unit;
    }
   

    public void OnRewardClicked(RewardDefinition reward)
    {
        switch (reward.targetType)
        {
            case RewardTargetType.None:
                ApplyRewardNoTarget(reward);
                break;

            case RewardTargetType.ChooseUnit:
                OpenEquipToUnitUI(reward);
                break;

            case RewardTargetType.RandomUnit:
                ApplyRewardToRandomUnit(reward);
                break;
        }
    }


    private void ApplyRewardNoTarget(RewardDefinition reward)
    {
        switch (reward.rewardType)
        {
            case RewardType.Gold:
                gold += GetGoldAmount(reward) ;
                break;

            case RewardType.InstantHeal:

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
                // 레벨업 중 각성으로 리스트가 변할 수 있으므로 스냅샷 복사 후 순회
                var expTargets = new List<GameObject>(playerUnits);
                foreach (var unitGO in expTargets)
                {
                    var unit = unitGO.GetComponent<Unit>();
                    if (unit == null) continue;

                    unit.GainLevel(reward.levelIncrease + levelPotionBonus);
                }
                break;
            
            case RewardType.Relic:

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
                Debug.LogWarning($"RewardType {reward.rewardType} 는 처리되지 않았습니다.");
                break;
        }
    }
    

    private void OpenEquipToUnitUI(RewardDefinition reward)
    {
        var unitList = new List<Unit>();
        foreach (var unitGO in playerUnits)
        {
            var u = unitGO.GetComponent<Unit>();
            if (u != null) unitList.Add(u);
        }

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

                pendingReward = null;
                FinishPending();
                rewardPhasePanel.gameObject.SetActive(true);
            }
        );
    }


    private void ApplyRewardToRandomUnit(RewardDefinition reward)
    {
        if (playerUnits.Count == 0)
        {
            Debug.LogWarning("플레이어 유닛이 없어 무작위 보상을 적용할 수 없습니다.");
            return;
        }

        int idx = UnityEngine.Random.Range(0, playerUnits.Count);
        GameObject targetGO = playerUnits[idx];
        Unit target = targetGO.GetComponent<Unit>();

        if (target == null)
        {
            Debug.LogWarning("랜덤 대상에 Unit 컴포넌트가 없습니다.");
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
            
            case RewardType.Revive:
                var unitfsm = unit.GetComponent<UnitFSM>();
                if(unitfsm != null) unitfsm.ReviveToEmptyTile(reward.reviveHerb);
                break;

            default:
                Debug.LogWarning($"RewardType {reward.rewardType} 은 Unit 대상 보상으로 처리되지 않습니다.");
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


    void SavePlayerFormation()
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

        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;
            
            fsm.ForceReady();
        }


        foreach (var go in enemyUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            fsm.ForceReady();
        }
    }    

    void TriggerEnterReadyHooks()
    {
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var status = go.GetComponent<UnitStatusEffectController>();
            if (status != null) status.OnEnterReadyState();
        }


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

        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;

            fsm.ForceIdle();
        }


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
        if (!isInBattle || isRunTerminated) return;

        if (!HasAliveEnemyUnit())
        {
            isInBattle = false;
            EndBattle(true);
        }
        else if (!HasAlivePlayerUnit())
        {
            isInBattle = false;
            EndBattle(false);
        }
    }

    private bool HasAlivePlayerUnit()
    {
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var unit = go.GetComponent<Unit>();
            if (unit != null && unit.hp > 0)
                return true;
        }

        return false;
    }

    private bool HasAliveEnemyUnit()
    {
        foreach (var go in enemyUnits)
        {
            if (go == null) continue;
            var unit = go.GetComponent<Unit>();
            if (unit != null && unit.hp > 0)
                return true;
        }

        return false;
    }

    private void ShowGameOverUI()
    {
        if (isRunTerminated) return;
        AudioManager.Instance?.PlayBgm(GameOverId, bgmFadeSeconds, false);
        isRunTerminated = true;

        var data = BuildResultData(false);
        NotifyGameManager(data);

        if (gameResultPanel != null)
        {
            gameResultPanel.Configure(RetryRun, GoToTitleScene);
            gameResultPanel.Show(data);
        }
        else
        {
            Debug.LogWarning("GameResultPanel가 설정되지 않아 결과 UI를 표시할 수 없습니다.");
        }
    }

    public void ShowGameClearUI()
    {
        if (isRunTerminated) return;
        AudioManager.Instance?.PlayBgm(GameClearId, bgmFadeSeconds, false);
        isRunTerminated = true;

        var data = BuildResultData(true);
        NotifyGameManager(data);

        if (gameResultPanel != null)
        {
            gameResultPanel.Configure(RetryRun, GoToTitleScene);
            gameResultPanel.Show(data);
        }
        else
        {
            Debug.LogWarning("GameResultPanel가 설정되지 않아 결과 UI를 표시할 수 없습니다.");
        }
    }

    private void RetryRun()
    {
        string scene = string.IsNullOrWhiteSpace(gameSceneName)
            ? SceneManager.GetActiveScene().name
            : gameSceneName;

        ReloadScene(scene);
    }

    private void GoToTitleScene()
    {
        string scene = string.IsNullOrWhiteSpace(titleSceneName)
            ? "StartScene"
            : titleSceneName;

        ReloadScene(scene);
    }

    private void ReloadScene(string sceneName)
    {
        Time.timeScale = 1f;
        Instance = null;
        Destroy(gameObject);
        SceneManager.LoadScene(sceneName);
    }

    private GameResultData BuildResultData(bool isClear)
    {
        int displayRound = Mathf.Max(1, currentLevel);
        double partyPower = CalculatePartyPower();
        var topUnit = GetTopStatUnit();

        // 클리어/패배 모두에서 가장 강한 우리 파티원 표시(패배 시 요구사항도 충족).
        double score = CalculateCompositeScore(displayRound, gold, totalEnemyKills, partyPower);

        return new GameResultData(
            isClear,
            displayRound,
            gold,
            totalEnemyKills,
            partyPower,
            topUnit.name,
            topUnit.portrait,
            score);
    }

    private double CalculatePartyPower()
    {
        double total = 0;

        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var unit = go.GetComponent<Unit>();
            if (unit == null) continue;

            total += unit.totalStrength + unit.totalAgility + unit.totalIntelligence;
        }

        return total;
    }

    private double CalculateCompositeScore(int round, int goldAmount, int kills, double partyPower)
    {
        double score =
            round * scoreRoundWeight +
            goldAmount * scoreGoldWeight +
            kills * scoreKillWeight +
            partyPower * scorePowerWeight;

        return System.Math.Round(score);
    }

    private (string name, Sprite portrait) GetTopStatUnit()
    {
        string topName = "";
        Sprite portrait = null;
        double topValue = double.MinValue;

        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var unit = go.GetComponent<Unit>();
            if (unit == null) continue;

            double score = unit.totalStrength + unit.totalAgility + unit.totalIntelligence;
            if (score > topValue)
            {
                topValue = score;
                topName = unit.unitName;
                portrait = unit.uiPortrait;
            }
        }

        return (topName, portrait);
    }

    private void NotifyGameManager(GameResultData data)
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.OnRunFinished(data);
        }
    }

    void RestorePlayerFormation()
    {
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm == null) continue;
            if (!fsm.gameObject.activeInHierarchy) continue;

            
            if (savedFormation.TryGetValue(fsm.unitId, out var tile))
            {
                fsm.ForceReady();
                fsm.SetPositionInstant(tile);                
            }
        }


    }


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


    void PlayBossIntroDialogue()
    {
        var line = enemySpawnManager.LastBossLine;
        switch (enemySpawnManager.LastBossCategory)
        {
            case BossCategory.Necromancer:
                dialogueManager.StartById($"NecromancerIntro_{enemySpawnManager.LastNecromancerIndex}");
                break;
            case BossCategory.BiomeBoss:
                dialogueManager.StartById($"BossIntro_{currentBiome}_{line}");
                break;
        }
    }



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

            SwitchBiomePersistentEffects(oldBiome, newBiome);
            ReviveAndHealPartyFull(); // 바이옴 전환 시 파티 전원 부활+회복
            CurrentBiome = newBiome;
            OnBiomeChanged?.Invoke(CurrentBiome);
        }
    }

    private BiomeType ResolveBiomeForRound(int round)
    {

        if (round >= 181)
            return BiomeType.Labyrinth;


        if (round <= 20)
            return fixedBiome_0_20;

        int segment = (round - 1) / 20;

        if (segment != _biomeSegmentIndex)
        {
            _biomeSegmentIndex = segment;
            var baseBiome = (_biomeSegmentIndex == int.MinValue) ? CurrentBiome : _biomeSegmentValue;
            _biomeSegmentValue = PickNextBiome(baseBiome);
        }

        return _biomeSegmentValue;
    }
    

    private static readonly BiomeType[] DefaultBiomePool =
    {
        BiomeType.Forest,
        BiomeType.Plains,
        BiomeType.DeepForest,
        BiomeType.Cave,
        BiomeType.Lake,
        BiomeType.Snow,
        BiomeType.Desert
    };

    private static readonly Dictionary<BiomeType, BiomeType[]> BiomeTransitions =
        new Dictionary<BiomeType, BiomeType[]>
    {
        { BiomeType.Forest,     new[] { BiomeType.Plains, BiomeType.DeepForest } },
        { BiomeType.Plains,     new[] { BiomeType.DeepForest, BiomeType.Lake } },
        { BiomeType.DeepForest, new[] { BiomeType.Cave, BiomeType.Snow } },
        { BiomeType.Cave,       new[] { BiomeType.Lake, BiomeType.Desert } },
        { BiomeType.Lake,       new[] { BiomeType.DeepForest, BiomeType.Forest } },
        { BiomeType.Snow,       new[] { BiomeType.Cave, BiomeType.Desert } },
        { BiomeType.Desert,     new[] { BiomeType.Plains } },
    };

    private static BiomeType PickNextBiome(BiomeType current)
    {
        if (!BiomeTransitions.TryGetValue(current, out var candidates) || candidates == null || candidates.Length == 0)
            candidates = DefaultBiomePool;

        return candidates[UnityEngine.Random.Range(0, candidates.Length)];
    }   

    // 바이옴이 바뀔 때 전체 부활 + 전체 회복
    private void ReviveAndHealPartyFull()
    {
        foreach (var unitGO in playerUnits)
        {
            if (unitGO == null) continue;

            var ufsm = unitGO.GetComponent<UnitFSM>();
            if (ufsm != null) ufsm.ReviveToEmptyTile(false); // false: 풀회복만, 현재 로직에서 부활 포함

            var unit = unitGO.GetComponent<Unit>();
            if (unit != null) unit.HealByPotion(0, 0, true); // full heal
        }
    }



    private void ApplyPendingPartyDebuffs()
    {
        if (pendingPartyDebuffs == null || pendingPartyDebuffs.Count == 0) return;

        foreach (var unitGO in playerUnits)
        {
            if (unitGO == null) continue;

            var fsm = unitGO.GetComponent<UnitFSM>();
            var status = unitGO.GetComponent<Component>();

            foreach (var d in pendingPartyDebuffs)
            {
                if (d == null) continue;

                if (d.type == PendingPartyDebuff.Type.Stun)
                {
                    if (fsm != null) fsm.ApplyStun(d.duration);
                    continue;
                }


                var sec = unitGO.GetComponent("UnitStatusEffectController");
                if (sec == null) continue;

                switch (d.type)
                {
                    case PendingPartyDebuff.Type.Poison:

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
