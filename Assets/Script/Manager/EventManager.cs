using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [SerializeField] private List<EventDefinition> eventPool; // 이벤트 SO 리스트
    [SerializeField] private EventPanel eventPanel;           // UI 패널

    public int GetPreviewGoldCost(EventChoice choice) => GetActualGoldCost(choice);

    // 등장횟수 조절
    private readonly Dictionary<string, int> _pickedCount = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 등급별 + 조건(라운드/바이옴/등장횟수)로 랜덤 뽑기
    public string PickRandomEventId()
    {
        var pickedRarity = RollRarity();

        int round = RunManager.Instance.CurrentLevel;
        BiomeType biome = RunManager.Instance.CurrentBiome;
        var list = FilterCandidates(pickedRarity, round, biome);

        // 해당 등급 이벤트가 비어있으면 Common으로 폴백
        if (list.Count == 0)
            list = FilterCandidates(EventRarity.Common, round, biome);

        if (list.Count == 0) return "";

        string pickedId = PickWeighted(list);

        // 뽑은 순간 카운트 등록
        RegisterPicked(pickedId);

        return pickedId;
    }

    // 후보 필터: 등급 + 라운드 범위 + 바이옴 + 반복 제한
    private List<EventDefinition> FilterCandidates(EventRarity rarity, int round, BiomeType biome)
    {
        var result = new List<EventDefinition>();

        foreach (var e in eventPool)
        {
            if (e == null) continue;
            if (e.rarity != rarity) continue;

            // 라운드 제한
            if (round < e.minRound || round > e.maxRound) continue;

            // 바이옴 제한 (allowedBiomes 비어있으면 전체 허용)
            if (e.allowedBiomes != null && e.allowedBiomes.Count > 0 && !e.allowedBiomes.Contains(biome))
                continue;

            // 등장 횟수 제한
            if (!CanAppear(e)) continue;

            result.Add(e);
        }

        return result;
    }

    private bool CanAppear(EventDefinition e)
    {
        if (string.IsNullOrEmpty(e.eventId)) return false;

        _pickedCount.TryGetValue(e.eventId, out int count);

        switch (e.repeatRule)
        {
            case EventRepeatRule.Unlimited:
                return true;

            case EventRepeatRule.OncePerRun:
                return count == 0;

            case EventRepeatRule.MaxTimesPerRun:
                return count < Mathf.Max(0, e.maxTimesPerRun);

            default:
                return true;
        }
    }

    private void RegisterPicked(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;

        _pickedCount.TryGetValue(eventId, out int count);
        _pickedCount[eventId] = count + 1;
    }

    // 등급 뽑기
    private EventRarity RollRarity()
    {
        // 전설 1%, 유니크 3%, 희귀 20%, 나머지 일반
        float r = Random.value;
        if (r < 0.01f) return EventRarity.Legendary;
        if (r < 0.01f + 0.03f) return EventRarity.Unique;
        if (r < 0.01f + 0.03f + 0.20f) return EventRarity.Rare;
        return EventRarity.Common;
    }

    // 가중치로 뽑기
    private string PickWeighted(List<EventDefinition> list)
    {
        int total = 0;
        foreach (var e in list) total += Mathf.Max(0, e.weight);
        if (total <= 0) return list.Count > 0 ? list[0].eventId : "";

        int r = Random.Range(0, total);
        int acc = 0;
        foreach (var e in list)
        {
            acc += Mathf.Max(0, e.weight);
            if (r < acc) return e.eventId;
        }
        return list[0].eventId;
    }

    // ID로 이벤트 정의 가져오기
    public EventDefinition GetById(string id)
    {
        return eventPool.Find(e => e != null && e.eventId == id);
    }

    // 이벤트 시작
    public void StartEvent(string eventId)
    {
        Debug.Log($"[StartEvent] try eventId = '{eventId}'");
        var def = GetById(eventId);
        if (def == null)
        {
            Debug.LogWarning($"EventDefinition not found: {eventId}");
            RunManager.Instance.GoToNextRound();
            return;
        }

        eventPanel.Open(def, OnChoiceSelected);
    }

    // 실제 골드 비용 계산 (스케일링 포함)
    private int GetActualGoldCost(EventChoice choice)
    {
        float goldCost = choice.baseGoldCost;

        if (choice.useScaledGoldCost)
        {
            int round = RunManager.Instance.currentLevel;
            float mul = Mathf.Pow(choice.costMultiplier, Mathf.Max(0, round - 1));
            goldCost *= mul;
        }

        int result = Mathf.CeilToInt(goldCost); // 비용: 올림 추천
        return Mathf.Max(0, result);
    }

    // 플레이어가 선택지 고름
    void OnChoiceSelected(EventDefinition def, EventChoice choice)
    {
        // 골드 체크
        int cost = GetActualGoldCost(choice);

        if (cost > 0 && RunManager.Instance.gold < cost)
        {
            ToastManager.Instance?.Show("골드가 부족합니다.");
            return;
        }

        RunManager.Instance.gold -= cost;

        // 간단 회복(부활은 outcome에서 처리하도록 분리)
        if (choice.healPartyFull)
        {
            foreach (var go in RunManager.Instance.playerUnits)
            {
                if (go == null) continue;
                var u = go.GetComponent<Unit>();
                if (u == null) continue;
                u.HealByPotion(0, 0, true);
            }
            ToastManager.Instance?.Show("파티가 회복되었습니다.");
        }

        if (choice.startBanditBattle)
        {
            eventPanel.Close();
            RunManager.Instance.StartEventBanditBattle(choice.banditPresetKey);
            return;
        }

        if (choice.leave)
        {
            eventPanel.Close();
            ToastManager.Instance?.Show("그냥 지나갔다..");
            RunManager.Instance.EnterShopOnlyFromLeave();
            return;
        }

        if (choice.startQuest && choice.questToStart != null)
        {
            QuestManager.Instance?.StartQuest(choice.questToStart);
        }

        // 이벤트 종료
        eventPanel.Close();

        // 이벤트용 보상 실행
        if (!choice.leave)
        {
            var outcome = ResolveOutcome(choice);
            if (outcome != null) ApplyOutcome(outcome);

            // outcome이 null이면 기본 보상풀로 안전 처리
            // 분기: 다음 이벤트로 이어지기(보상 단계 스킵)
            if (outcome != null && !string.IsNullOrEmpty(outcome.nextEventId))
            {
                StartEvent(outcome.nextEventId);
                return;
            }

            // 분기: 보상 없이 바로 다음 라운드로
            if (outcome != null && outcome.skipRewardAndGoNextRound)
            {
                RunManager.Instance.GoToNextRound();
                return;
            }


            if (outcome != null)
                RunManager.Instance.EnterRewardFromEvent(outcome.rewardEventIdOverride);
            else
                RunManager.Instance.EnterRewardFromEvent(null);

            return;
        }
    }

    private EventOutcome ResolveOutcome(EventChoice choice)
    {
        if (choice == null) return null;

        // 1) 확률 결과들(RandomChance)만 있으면 chance로 굴림(누적확률)
        var chanceList = choice.outcomes.FindAll(o => o != null && o.rollType == EventRollType.RandomChance);
        if (chanceList.Count > 0)
        {
            float r = Random.value;
            float acc = 0f;

            foreach (var o in chanceList)
            {
                acc += Mathf.Clamp01(o.chance);
                if (r <= acc) return o;
            }

            return chanceList[chanceList.Count - 1];
        }

        // 2) 스탯 판정(힘/민첩/지능) : 큰 배수부터 체크해서 1등/2등 처럼 분기
        int round = RunManager.Instance.CurrentLevel;

        var strChecks = choice.outcomes.FindAll(o => o != null && o.rollType == EventRollType.PartyStrCheck);
        var agiChecks = choice.outcomes.FindAll(o => o != null && o.rollType == EventRollType.PartyAgiCheck);
        var intChecks = choice.outcomes.FindAll(o => o != null && o.rollType == EventRollType.PartyIntCheck);

        if (strChecks.Count > 0)
        {
            int party = SumPartyTotalStrength();
            return ResolveStatCheck(round, party, strChecks, choice);
        }
        if (agiChecks.Count > 0)
        {
            int party = SumPartyTotalAgility();
            return ResolveStatCheck(round, party, agiChecks, choice);
        }
        if (intChecks.Count > 0)
        {
            int party = SumPartyTotalIntelligence();
            return ResolveStatCheck(round, party, intChecks, choice);
        }

        // 3) 기본(첫 outcome) or null
        return (choice.outcomes != null && choice.outcomes.Count > 0) ? choice.outcomes[0] : null;
    }

    private EventOutcome ResolveStatCheck(int round, int partyStat, List<EventOutcome> checks, EventChoice choice)
    {
        // 큰 배수부터 우선
        checks.Sort((a, b) => b.statMultiplier.CompareTo(a.statMultiplier));

        foreach (var o in checks)
        {
            int need = round * Mathf.Max(0, o.statMultiplier);
            if (partyStat >= need) return o;
        }

        // 실패시: rollType None인 outcome이 있으면 그거, 아니면 첫 outcome
        var fallback = choice.outcomes.Find(o => o != null && o.rollType == EventRollType.None);
        if (fallback != null) return fallback;

        return (choice.outcomes != null && choice.outcomes.Count > 0) ? choice.outcomes[0] : null;
    }

    private int SumPartyTotalStrength()
    {
        double sum = 0;
        foreach (var go in RunManager.Instance.playerUnits)
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            sum += u.totalStrength;
        }
        return Mathf.FloorToInt((float)sum);
    }

    private int SumPartyTotalAgility()
    {
        double sum = 0;
        foreach (var go in RunManager.Instance.playerUnits)
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            sum += u.totalAgility;
        }
        return Mathf.FloorToInt((float)sum);
    }

    private int SumPartyTotalIntelligence()
    {
        double sum = 0;
        foreach (var go in RunManager.Instance.playerUnits)
        {
            if (go == null) continue;
            var u = go.GetComponent<Unit>();
            if (u == null) continue;
            sum += u.totalIntelligence;
        }
        return Mathf.FloorToInt((float)sum);
    }

    private void ApplyOutcome(EventOutcome outcome)
    {
        if (outcome == null) return;

        // ===== 즉시 효과 =====
        foreach (var go in RunManager.Instance.playerUnits)
        {
            if (go == null) continue;

            var u = go.GetComponent<Unit>();
            var fsm = go.GetComponent<UnitFSM>();

            if (outcome.reviveAllFainted && fsm != null)
            {
                // false = 전체회복 + 부활 (RunManager.EnterRest 주석 기준)
                fsm.ReviveToEmptyTile(true);
            }

            if (u == null) continue;

            if (outcome.healPartyFull)
            {
                u.HealByPotion(0, 0, true);
            }

            if (outcome.healPartyByMaxHpPercent)
            {
                double amount = u.maxHp * Mathf.Clamp01(outcome.healMaxHpPercent);
                u.Heal(amount);
            }

            if (outcome.damagePartyByCurrentHpPercent)
            {
                double dmg = u.hp * Mathf.Clamp01(outcome.damageCurrentHpPercent);

                if (outcome.nonLethalDamage)
                {
                    // HP 1은 남김
                    dmg = System.Math.Min(dmg, System.Math.Max(0, u.hp - 1));
                }

                u.TakeDamage(dmg);
            }

            if (outcome.restorePartyManaTo100)
            {
                u.mp = Mathf.Min(u.maxMp, 100f);
            }

            if (outcome.restorePartyManaFlat)
            {
                u.mp = Mathf.Min(u.maxMp, u.mp + outcome.manaFlat);
            }

            // ===== 즉시 골드 지급 ====
            if (outcome.addGold)
            {
                int min = Mathf.Min(outcome.goldMin, outcome.goldMax);
                int max = Mathf.Max(outcome.goldMin, outcome.goldMax);
                int baseGain = Random.Range(min, max + 1);

                float roundMul = (outcome.goldRoundMultiplier <= 0f) ? 1.10f : outcome.goldRoundMultiplier;

                int gain = RunManager.Instance.GetScaledGoldAmount(
                    baseGain,
                    outcome.scaleGoldWithRound,
                    roundMul
                );

                RunManager.Instance.gold += gain;
                ToastManager.Instance?.Show($"+{gain} 골드를 얻었다!!", 0.5f);
            }

            // ===== 현재 소지금 기반 골드 연산(도박장/올인) 1회 적용 =====
            if (outcome.modifyCurrentGold)
            {
                int g = RunManager.Instance.gold;

                if (outcome.setGoldToZero)
                {
                    RunManager.Instance.gold = 0;
                    ToastManager.Instance?.Show("골드를 전부 잃었다..", 0.5f);
                }
                else
                {
                    if (outcome.loseGoldByPercent && outcome.loseGoldPercent > 0f)
                    {
                        int lose = Mathf.RoundToInt(g * Mathf.Clamp01(outcome.loseGoldPercent));
                        g = Mathf.Max(0, g - lose);
                        ToastManager.Instance?.Show($"{lose} 골드를 잃었다..", 0.5f);
                    }

                    if (outcome.multiplyGold && outcome.goldMultiplier > 0f)
                    {
                        g = Mathf.Max(0, Mathf.RoundToInt(g * outcome.goldMultiplier));
                        ToastManager.Instance?.Show($"성공 : {g}골드를 얻었다!!", 0.5f);
                    }

                    RunManager.Instance.gold = g;
                }
            }
        }

        // ===== 다음 전투 적 레벨 보정 =====
        if (outcome.increaseNextBattleEnemyLevel && outcome.nextBattleEnemyLevelBonus != 0)
        {
            RunManager.Instance.AddNextBattleEnemyLevelOffset(outcome.nextBattleEnemyLevelBonus);
        }

        // ===== 다음 전투 파티 패널티 예약 =====
        if (outcome.addNextBattlePartyStun)
            RunManager.Instance.AddPendingPartyStun(outcome.stunDuration);

        if (outcome.addNextBattlePartyPoison)
            RunManager.Instance.AddPendingPartyPoison(outcome.poisonDuration, outcome.poisonDpsRatioOfMaxHp);

        if (outcome.addNextBattlePartyBurnAmp)
            RunManager.Instance.AddPendingPartyBurnAmp(outcome.burnAmpDuration, outcome.burnAmpMultiplier);

        if (outcome.addNextBattlePartyMoveSlow)
            RunManager.Instance.AddPendingPartyMoveSlow(outcome.moveSlowDuration, outcome.moveSlowMultiplier);

        if (outcome.addNextBattlePartyAttackSlow)
            RunManager.Instance.AddPendingPartyAttackSlow(outcome.attackSlowDuration, outcome.attackSlowMultiplier);
    }
}