using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    [SerializeField] private double[] levelUpExpTable;
    private double battleExpPool;
    private double battleGoldPool;

    public static RunManager Instance { get; private set; }
    public RunState currentRunState {get; private set;}
    public int currentLevel;
    public int CurrentLevel => currentLevel;
    public int gold;

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
        currentLevel = 0;
        gold = 1000;
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

    public void SelectNode(MapNode node)
    {
        currentNodeType = node.Type;
        currentLevel = node.Level;
        int effectiveRound = Mathf.Max(1, currentLevel); // 0라운드는 1라운드 스케일로 취급
        UpdateBiomeByRound(effectiveRound);
        QuestManager.Instance?.OnRoundAdvanced();
        currentEventId = "";


        switch (currentNodeType)
        {
            case NodeType.Combat:
                mapGenerator.ToggleMapView();
                EnterReady();                
                break;
            case NodeType.Boss:
                mapGenerator.ToggleMapView();
                EnterReady();

                break;
            case NodeType.Event:
                mapGenerator.ToggleMapView();
                EnterEvent(node);

                break;
            case NodeType.Rest:
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





        battleExpPool = 0;
        battleGoldPool = 0;
        EnsureLevelUpExpTable();


        if (currentNodeType == NodeType.Boss)
        {
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
    
    public void StartBattle()
    {
        if (currentRunState != RunState.Ready)
            return;

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



        }
    }

    public void OnEnemyDefeated(GameObject enemyGO)
    {
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

        ToastManager.Instance?.Show("모든 아군이 회복되었습니다!");
        mapGenerator.ToggleMapView();
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



        mapGenerator.ToggleMapView();
        currentRunState = RunState.OnMap;        
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
            mapGenerator.ToggleMapView();
        }
       
    }


    public GameObject SpawnUnit(UnitData data)
    {

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
                foreach (var unitGO in playerUnits)
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




