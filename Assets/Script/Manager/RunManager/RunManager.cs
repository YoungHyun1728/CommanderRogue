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


public class RunManager : MonoBehaviour
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
        set => currentBiome = value;
    }
    public event System.Action<BiomeType> OnBiomeChanged;
    private int _biomeSegmentIndex = int.MinValue;
    private BiomeType _biomeSegmentValue = BiomeType.Forest;

    [Header("경험치/골드")]
    [SerializeField] private float enemyExpFraction = 0.45f;
    [SerializeField] private float enemyGoldCoefficient = 77;
    [SerializeField] private int baseStartGold = 1000;
    [SerializeField] private double[] levelUpExpTable;
    public double battleExpPool;
    public double battleGoldPool;

    [System.Serializable]
    private class AwakenPair
    {
        public UnitData baseForm;      // 기본 유닛데이터
        public UnitData awakenedForm;  // 각성 유닛데이터
    }

    [Header("각성 매핑")]
    [SerializeField] private List<AwakenPair> awakenPairs = new();

    public static RunManager Instance { get; private set; }
    public RunState currentRunState {get; set;}
    public int currentLevel;
    public int CurrentLevel => currentLevel;
    public List<UnitData> PlayerUnitPool => playerUnitPool;
    public ChooseUnitPanel ChooseUnitPanel => chooseUnitPanel;
    public MapGenerator MapGenerator => mapGenerator;
    public TileMapManager TileMapManager => tileMapManager;
    public RewardManager RewardManager => rewardManager;
    public RewardPhasePanel RewardPhasePanel => rewardPhasePanel;
    public float EnemyExpFraction => enemyExpFraction;
    public float EnemyGoldCoefficient => enemyGoldCoefficient;
    public int RerollBaseCostPerRound => rerollBaseCostPerRound;
    public int RerollCostStep => rerollCostStep;
    // 챌린지 표시용 프로퍼티는 코디네이터 상태를 그대로 노출한다.
    public bool ChallengeModeActive => challengeCoordinator != null && challengeCoordinator.ChallengeModeActive;
    public string ChallengePartyName => challengeCoordinator != null ? challengeCoordinator.ChallengePartyName : "";
    public string ChallengeOpponentName => challengeCoordinator != null ? challengeCoordinator.ChallengeOpponentName : "";
    public int gold;

    [Header("결과/통계")]
    [SerializeField] private GameResultPanel gameResultPanel;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string titleSceneName = "StartScene";
    [SerializeField] private float scoreRoundWeight = 100f;
    [SerializeField] private float scoreGoldWeight = 0.01f;
    [SerializeField] private float scoreKillWeight = 20f;
    [SerializeField] private float scorePowerWeight = 1f;
    public int totalEnemyKills;
    public bool isRunTerminated;

    [System.Serializable]
    public class PendingPartyDebuff
    {
        public enum Type { Stun, Poison, BurnAmp, MoveSlow, AttackSlow }
        public Type type;

        public float duration;
        public float dpsRatioOfMaxHp;
        public float multiplier;
    }

    public readonly List<PendingPartyDebuff> pendingPartyDebuffs = new();

    public List<GameObject> playerUnits = new List<GameObject>();
    public List<GameObject> enemyUnits = new List<GameObject>();

    private Dictionary<int, Vector2Int> savedFormation = new Dictionary<int, Vector2Int>(); 
    
    public NodeType currentNodeType { get; set; }
    public string currentEventId { get; set; }

    public bool isInBattle;
    public bool isInEvent;
    public bool isInReward;

    [Header("준비 UI 참조")]
    [SerializeField] private Button fleeButton;

    [Header("전투 회피 설정")]
    [Range(0f, 1f)] [SerializeField] private float fleeSuccessRate = 0.35f;


    public int rerollCountThisRound = 0;
    [SerializeField] private int rerollBaseCostPerRound = 200;
    [SerializeField] private int rerollCostStep = 2;


    public RewardDefinition pendingReward = null;
    public bool pendingWasFreeReward = false;
    public bool pendingIsShop;
    public int pendingShopCost;

    public int GatherHeroBuyCount = 0;
    [SerializeField] private bool usePriceTiers;
    [SerializeField] private List<int> priceTiers;


    public int levelPotionBonus;
    public int expAmulet;
    public int goldAmulet;


    public int nextBattleEnemyLevelOffset = 0;
    [SerializeField] private List<UnitData> challengeEnemyUnitPool = new();
    
    // RunManager는 상태 전환 오케스트레이션만 담당하고,
    // 세부 도메인 로직은 전용 코디네이터/컨트롤러에 위임한다.
    public RunBiomeEffectsController biomeEffects;
    private RunChallengeCoordinator challengeCoordinator;
    private RunBattleCoordinator battleCoordinator;
    private RunRewardCoordinator rewardCoordinator;
    private RunSaveCoordinator saveCoordinator;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            biomeEffects = new RunBiomeEffectsController(this);
            challengeCoordinator = new RunChallengeCoordinator();
            battleCoordinator = new RunBattleCoordinator(this);
            rewardCoordinator = new RunRewardCoordinator(this);
            saveCoordinator = new RunSaveCoordinator(this);
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

    public void StartNewRun()
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
        challengeCoordinator?.ResetForNewRun();
        
        GainUnit();

        biomeEffects?.ApplyPersistentToParty(CurrentBiome);

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

    public void PlayBossBattleBgm()
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

        if (challengeCoordinator != null && challengeCoordinator.IsChallengeCombatNode(currentNodeType))
        {
            mapGenerator.MapViewOff();
            StartCoroutine(FetchChallengeAndEnter(biomeChanged || isFirstNode));
            return;
        }

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
        
        DespawnCurrentEnemies();

        bool spawnedChallenge = challengeCoordinator != null
            && challengeCoordinator.TrySpawnChallengeEnemies(
                enemySpawnManager,
                challengeEnemyUnitPool,
                ResolveEquipmentByName);

        if (spawnedChallenge)
        {
            // 챌린지 전투는 코디네이터가 가져온 스냅샷으로 스폰한다.
        }
        else
        {
            int spawnRound = Mathf.Max(1, currentLevel); // 0라운드도 1라운드 스케일로 스폰
            enemySpawnManager.SpawnBattle(currentBiome, spawnRound, currentNodeType == NodeType.Boss, nextBattleEnemyLevelOffset);
        }

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

    private void DespawnCurrentEnemies()
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

    public void RefreshFleeButton()
    {
        if (fleeButton == null) return;

        bool show = currentRunState == RunState.Ready && currentNodeType == NodeType.Combat;
        fleeButton.gameObject.SetActive(show);
        if (show) fleeButton.interactable = true;
    }
    
    public void StartBattle()
    {
        // 전투 상세 로직은 RunBattleCoordinator가 담당한다.
        battleCoordinator?.StartBattle();
    }

    /// <summary>
    /// 챌린지 전투 진입 전 상대 스냅샷을 비동기로 준비한다.
    /// BGM과 상태 전이는 RunManager가, 스냅샷 수급은 코디네이터가 담당한다.
    /// </summary>
    private IEnumerator FetchChallengeAndEnter(bool playBgm)
    {
        PlayBiomeBgm(playBgm);
        if (challengeCoordinator != null)
            yield return StartCoroutine(challengeCoordinator.FetchAndCacheNextOpponent());

        EnterReady();
    }

    public void OnEnemyDefeated(GameObject enemyGO)
    {
        battleCoordinator?.OnEnemyDefeated(enemyGO);
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
        // 이벤트 진입/정산은 RunRewardCoordinator로 위임.
        rewardCoordinator?.EnterRewardFromEvent(overrideEventId);
    }


    public void EnterShopOnlyFromLeave()
    {
        // Leave 선택 후 "상점만" 진입하는 특수 보상 플로우.
        rewardCoordinator?.EnterShopOnlyFromLeave();
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

    public void EnterReward()
    {
        // 일반 전투 승리 후 보상 진입.
        rewardCoordinator?.EnterReward();
    }

    public void GoToNextRound()
    {
        // 맵 복귀 + 자동 저장은 RunSaveCoordinator 담당.
        saveCoordinator?.GoToNextRound();
    }

    private SaveData BuildSaveData(MapGenerator mapGen)
    {
        return saveCoordinator != null ? saveCoordinator.BuildSaveData(mapGen) : null;
    }

    private void RestoreRun(SaveData data)
    {
        saveCoordinator?.RestoreRun(data);
    }

    // 설정창 등에서 호출: 현재 진행 상태를 저장하고 타이틀로 복귀
    public void SaveAndReturnToTitle()
    {
        saveCoordinator?.SaveAndReturnToTitle();
    }

    public Equipment ResolveEquipmentByName(string name)
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

    public int GetShopPrice(RewardDefinition r)
    {
        return rewardCoordinator != null ? rewardCoordinator.GetShopPrice(r) : 0;
    }


    public int GetGoldAmount(RewardDefinition r)
    {
        return rewardCoordinator != null ? rewardCoordinator.GetGoldAmount(r) : 0;
    }

    public int GetScaledGoldAmount(float baseGold, bool scaleWithRound, float roundMultiplier)
    {
        return rewardCoordinator != null
            ? rewardCoordinator.GetScaledGoldAmount(baseGold, scaleWithRound, roundMultiplier)
            : 0;
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

    // ===== Debug: Add random player unit =====
    [ContextMenu("Debug/Add Random Player Unit")]
    private void DebugAddRandomPlayerUnit()
    {
        if (playerUnitPool == null || playerUnitPool.Count == 0)
        {
            Debug.LogWarning("[Debug] playerUnitPool이 비어 있어 유닛을 추가할 수 없습니다.");
            return;
        }

        var data = ScriptableObject.Instantiate(playerUnitPool[Random.Range(0, playerUnitPool.Count)]);
        data.isPlayerUnit = true;

        Vector2Int tile;
        if (tileMapManager != null && tileMapManager.GetEmptyTile(out tile))
        {
            SpawnUnitAtTile(data, tile);
            Debug.Log($"[Debug] 유닛 추가: {data.unitName} at {tile}");
        }
        else
        {
            SpawnUnit(data);
            Debug.Log($"[Debug] 유닛 추가(기본 위치): {data.unitName}");
        }
    }

    // baseData를 복제한 뒤 각성 템플릿(스킬/프리팹/이름 등)만 덮어쓴 최종 데이터 반환
    // keepBaseName=true면 name/unitName을 원본 그대로 유지(스냅샷/로그 혼동 방지)
    public UnitData ResolveAwakenedData(UnitData baseData, bool keepBaseName = false)
    {
        if (baseData == null) return null;

        var pair = awakenPairs.Find(p =>
            p != null && p.baseForm != null &&
            (p.baseForm == baseData ||
             p.baseForm.name == baseData.name ||
             p.baseForm.unitName == baseData.unitName));
        if (pair == null || pair.awakenedForm == null)
            return baseData;

        var clone = ScriptableObject.Instantiate(baseData);
        var template = pair.awakenedForm;
        clone.isPlayerUnit = baseData.isPlayerUnit;

        if (!keepBaseName)
        {
            // 이름(에셋/표시)을 각성 템플릿으로 덮어 base ↔ awakened 구분이 저장/스냅샷에 반영되도록 한다.
            if (!string.IsNullOrEmpty(template.name)) clone.name = template.name;
            if (!string.IsNullOrEmpty(template.unitName)) clone.unitName = template.unitName;
        }
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
        var carriedEquipments = new List<Equipment>();
        if (unit.equippedItems != null)
        {
            foreach (var eq in unit.equippedItems)
            {
                if (eq != null) carriedEquipments.Add(eq);
            }
        }

        // 기존 유닛 정리
        tileMapManager.ReleaseUnitAll(fsmOld.unitId);
        playerUnits.Remove(unit.gameObject);
        tileMapManager.playerUnits.Remove(unit.gameObject);
        Destroy(unit.gameObject);

        // 각성 데이터 준비
        var awakenedData = ResolveAwakenedData(pair.baseForm);
        awakenedData.level = unit.level; // 레벨 유지
        awakenedData.isPlayerUnit = true;
        if (carriedEquipments.Count > 0)
        {
            // 각성 시 기존 장착 장비를 보존한다.
            awakenedData.startingEquipments = new List<Equipment>(carriedEquipments);
        }

        // 동일 타일에 스폰
        SpawnUnitAtTile(awakenedData, tile);
    }

    public GameObject SpawnUnitAtTile(UnitData data, Vector2Int tile)
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
        rewardCoordinator?.OnRewardClicked(reward);
    }

    public void SavePlayerFormation()
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

    private void AllUnitsReady()
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

    private void TriggerEnterReadyHooks()
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

    public void ResetBattleFlagsForAllUnits()
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

    public void AllUnitsIdle()
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

    public void EnemyUnitsIdle()
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
        battleCoordinator?.CheckEndBattle();
    }

    public void ShowGameOverUI()
    {
        if (isRunTerminated) return;
        AudioManager.Instance?.PlayBgm(GameOverId, bgmFadeSeconds, false);
        isRunTerminated = true;

        var data = BuildResultData(false);
        NotifyGameManager(data);
        SaveManager.instance?.DeleteSaveFiles();

        if (gameResultPanel != null)
        {
            gameResultPanel.Configure(RetryRun, GoToTitleScene, OnChallengeModeRequested);
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

        if (currentLevel == 250) isRunTerminated = true;      

        var data = BuildResultData(true);
        NotifyGameManager(data);
        SaveManager.instance?.DeleteSaveFiles();

        if (gameResultPanel != null)
        {
            gameResultPanel.Configure(RetryRun, GoToTitleScene, OnChallengeModeRequested);
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

    public void GoToTitleScene()
    {
        string scene = string.IsNullOrWhiteSpace(titleSceneName)
            ? "StartScene"
            : titleSceneName;

        ReloadScene(scene);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, layer);
        }
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

        // 클리어/패배시 가장 강한 우리 파티원 표시
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

    /// <summary>
    /// 클리어 UI의 "챌린지 시작" 요청을 처리한다.
    /// 챌린지 데이터 등록 자체는 RunChallengeCoordinator가 담당하고,
    /// RunManager는 맵/상태 전환만 수행한다.
    /// </summary>
    private void OnChallengeModeRequested(string partyName)
    {
        if (challengeCoordinator == null) return;

        if (!challengeCoordinator.TryActivateFromCurrentParty(this, partyName, out var error))
        {
            if (!string.IsNullOrEmpty(error))
            {
                if (error.Contains("최대 10자"))
                    ToastManager.Instance?.Show(error, 0.4f, 0.2f);
                else
                    Debug.LogError(error);
            }
            return;
        }

        if (mapGenerator != null)
        {
            mapGenerator.StartChallengeMap(mapGenerator.ChallengeMaxLevel);
            mapGenerator.MapViewOn();
        }

        currentRunState = RunState.OnMap;
        isInBattle = false;
        isInEvent = false;
        isInReward = false;

        if (gameResultPanel != null) gameResultPanel.HideImmediate();
    }

    

    [ContextMenu("Debug/Upload Current Party Once")]
    private void DebugUploadCurrentPartyOnce()
    {
        DebugUploadChallengeCopies(1);
    }

    private void DebugUploadChallengeCopies(int count)
    {
        challengeCoordinator?.DebugUploadChallengeCopies(this, count);
    }

    /// <summary>
    /// 현재 씬에서 바로 챌린지 전투를 시작한다.
    /// </summary>

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

    public void RestorePlayerFormation()
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

    public void EnsureLevelUpExpTable()
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



    /// <summary>
    /// 바이옴/전투 보조 서비스가 재사용하는 파티 Unit 목록 조회.
    /// </summary>
    public List<Unit> GetPartyUnitComponents()
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

    /// <summary>
    /// 바이옴/전투 보조 서비스가 재사용하는 적 Unit 목록 조회.
    /// </summary>
    public List<Unit> GetEnemyUnitComponents()
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

            biomeEffects?.SwitchPersistentEffects(oldBiome, newBiome);
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
    public void ReviveAndHealPartyFull()
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



    public void ApplyPendingPartyDebuffs()
    {
        if (pendingPartyDebuffs == null || pendingPartyDebuffs.Count == 0) return;

        foreach (var unitGO in playerUnits)
        {
            if (unitGO == null) continue;

            var fsm = unitGO.GetComponent<UnitFSM>();
            var status = unitGO.GetComponent<UnitStatusEffectController>();
            var unit = unitGO.GetComponent<Unit>();

            foreach (var d in pendingPartyDebuffs)
            {
                if (d == null) continue;

                if (d.type == PendingPartyDebuff.Type.Stun)
                {
                    if (fsm != null) fsm.ApplyStun(d.duration);
                    continue;
                }
                if (status == null) continue;

                switch (d.type)
                {
                    case PendingPartyDebuff.Type.Poison:
                        {
                            // 이벤트 결과는 비율로 저장되므로 유닛별 최대 체력 기준 dps로 환산한다.
                            double dps = unit != null ? unit.maxHp * d.dpsRatioOfMaxHp : d.dpsRatioOfMaxHp;
                            status.ApplyPoison(dps, d.duration);
                        }
                        break;

                    case PendingPartyDebuff.Type.BurnAmp:
                        status.ApplyBurnAmp(d.multiplier, d.duration);
                        break;

                    case PendingPartyDebuff.Type.MoveSlow:
                        status.ApplyMoveSlow(NormalizeSlowMultiplier(d.multiplier), d.duration);
                        break;

                    case PendingPartyDebuff.Type.AttackSlow:
                        status.ApplyAttackSlow(NormalizeSlowMultiplier(d.multiplier), d.duration);
                        break;
                }
            }
        }

        pendingPartyDebuffs.Clear();
    }


    public bool HasPendingPartyStunAtBattleStart()
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

    private static float NormalizeSlowMultiplier(float multiplier)
    {
        if (multiplier <= 0f) return 1f;
        if (multiplier > 1f) return 1f / multiplier;
        return multiplier;
    }

}
