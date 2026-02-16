using System.Collections.Generic;
using UnityEngine;

public class RewardManager : MonoBehaviour
{
    [System.Serializable]
    public class RewardRarityGuarantee
    {
        public int mythic = 0;
        public int legendary = 0;
        public int rare = 0;
        public int special = 0;
    }

    /// <summary>
    /// 보상 풀 = (보상 리스트) + (보장 룰) + (풀 정책)
    /// </summary>
    [System.Serializable]
    public class RewardPool
    {
        [Tooltip("이벤트 풀에서는 eventId로 사용. 노드풀은 비워도 됨.")]
        public string key;

        public List<RewardDefinition> rewards = new();

        [Header("등급별 보장된 갯수")]
        public RewardRarityGuarantee guarantee = new();

        [Header("정책")]
        [Tooltip("0이면 제한 없음. 1이면 이 풀에서는 최대 1개만 뽑고 나머지는 global로 채움.")]
        public int maxPicksFromThisPool = 0;

        [Tooltip("이 풀에서 부족하면 globalPool로 채울지 여부")]
        public bool fillRestFromGlobal = true;
    }

    [Header("Pools")]
    [SerializeField] private RewardPool globalPool; // 전체풀
    [SerializeField] private RewardPool questPool;  // NodeType.Event 기본 풀
    [SerializeField] private RewardPool bossPool;   // NodeType.Boss 풀

    [Header("Event Pools (key == eventId)")]
    [SerializeField] private List<RewardPool> eventPools = new();

    // -----------------------------
    // Public API
    // -----------------------------
    public List<RewardDefinition> GetRewardChoices(
        int round,
        int rewardCount,
        NodeType nodeType,
        string eventId,
        bool forceGlobalPool = false)
    {
        RewardPool pool = ResolvePool(nodeType, eventId, forceGlobalPool);

        // 1) 우선 선택 풀에서 뽑기 (보장 + 확률)
        var picked = PickFromPoolInternal(round, rewardCount, pool);

        // 2) 부족분을 global에서 채우기
        if (picked.Count < rewardCount)
        {
            bool shouldFillFromGlobal = (pool != null && pool.fillRestFromGlobal);
            if (shouldFillFromGlobal)
                FillFromGlobal(round, rewardCount, picked);
        }

        return picked;
    }

    /// <summary>
    /// 상점 아이템은 globalPool에서 canAppearInShop으로 필터링
    /// </summary>
    public List<RewardDefinition> GetShopItems(int round)
    {
        var result = new List<RewardDefinition>();
        var src = globalPool != null ? globalPool.rewards : null;
        if (src == null) return result;

        foreach (var r in src)
        {
            if (r == null) continue;
            if (!r.canAppearInShop) continue;
            if (round < r.minRound || round > r.maxRound) continue;
            result.Add(r);
        }

        return result;
    }

    // -----------------------------
    // Pool resolve
    // -----------------------------
    private RewardPool ResolvePool(NodeType nodeType, string eventId, bool forceGlobalPool)
    {
        if (forceGlobalPool) return globalPool;

        // 이벤트 키가 있으면 이벤트 풀 우선
        if (!string.IsNullOrEmpty(eventId))
        {
            var match = eventPools.Find(p => p != null && p.key == eventId);
            if (match != null && match.rewards != null && match.rewards.Count > 0)
                return match;
        }

        // 노드 타입 기반 폴백
        if (nodeType == NodeType.Boss && bossPool != null && bossPool.rewards.Count > 0) return bossPool;
        if (nodeType == NodeType.Event && questPool != null && questPool.rewards.Count > 0) return questPool;

        return globalPool;
    }

    // -----------------------------
    // Pick logic (this pool only)
    // -----------------------------
    private List<RewardDefinition> PickFromPoolInternal(int round, int rewardCount, RewardPool pool)
    {
        var picked = new List<RewardDefinition>();
        if (pool == null || pool.rewards == null) return picked;

        // 이 풀에서 최대 몇 개 뽑을지 제한
        int targetCount = rewardCount;
        if (pool.maxPicksFromThisPool > 0)
            targetCount = Mathf.Min(rewardCount, pool.maxPicksFromThisPool);

        // 후보 생성
        var candidates = BuildCandidates(round, pool.rewards);
        if (candidates.Count == 0) return picked;

        // 1) 보장 먼저 (targetCount를 넘기지 않게)
        var g = pool.guarantee;
        if (g != null)
        {
            PickGuaranteed(candidates, picked, ItemRarity.Mythic, g.mythic, targetCount);
            PickGuaranteed(candidates, picked, ItemRarity.Legendary, g.legendary, targetCount);
            PickGuaranteed(candidates, picked, ItemRarity.Rare, g.rare, targetCount);
            PickGuaranteed(candidates, picked, ItemRarity.Special, g.special, targetCount);
        }

        // 2) 나머지 슬롯은 확률 롤로 채움 (targetCount까지)
        while (picked.Count < targetCount && candidates.Count > 0)
        {
            ItemRarity rarity = RollRarity_Global();

            RewardDefinition item = PickOneByRarity(candidates, rarity);

            // 폴백: 해당 등급이 없으면 다른 등급에서라도 채움
            if (item == null)
            {
                item = PickOneByRarity(candidates, ItemRarity.Special)
                    ?? PickOneByRarity(candidates, ItemRarity.Common)
                    ?? PickOneByRarity(candidates, ItemRarity.Rare)
                    ?? PickOneByRarity(candidates, ItemRarity.Legendary)
                    ?? PickOneByRarity(candidates, ItemRarity.Mythic);
            }

            if (item == null) break;

            picked.Add(item);
            candidates.Remove(item);
        }

        return picked;
    }

    private List<RewardDefinition> BuildCandidates(int round, List<RewardDefinition> source)
    {
        var candidates = new List<RewardDefinition>();
        if (source == null) return candidates;

        foreach (var r in source)
        {
            if (r == null) continue;
            if (!r.canAppearAsReward) continue;
            if (round < r.minRound || round > r.maxRound) continue;
            candidates.Add(r);
        }

        return candidates;
    }

    private void PickGuaranteed(
        List<RewardDefinition> candidates,
        List<RewardDefinition> picked,
        ItemRarity rarity,
        int count,
        int maxCount)
    {
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            if (picked.Count >= maxCount) return;

            var item = PickOneByRarity(candidates, rarity);
            if (item == null) return;

            picked.Add(item);
            candidates.Remove(item);
        }
    }

    private RewardDefinition PickOneByRarity(List<RewardDefinition> pool, ItemRarity rarity)
    {
        if (pool == null || pool.Count == 0) return null;

        var list = new List<RewardDefinition>();
        foreach (var r in pool)
        {
            if (r == null) continue;
            if (r.rarity == rarity) list.Add(r);
        }

        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    /// <summary>
    /// 전체풀 기준 등급 확률 테이블
    /// Mythic    1.12%
    /// Legendary 4.5%
    /// Rare      15.5%
    /// Special   33%
    /// Common    나머지
    /// </summary>
    private ItemRarity RollRarity_Global()
    {
        float r = Random.value;

        if (r < 0.0112f) return ItemRarity.Mythic;
        if (r < 0.0112f + 0.045f) return ItemRarity.Legendary;
        if (r < 0.0112f + 0.045f + 0.155f) return ItemRarity.Rare;
        if (r < 0.0112f + 0.045f + 0.155f + 0.33f) return ItemRarity.Special;
        return ItemRarity.Common;
    }

    // -----------------------------
    // Option A: Fill 부족분을 global로 채움
    // -----------------------------
    private void FillFromGlobal(int round, int rewardCount, List<RewardDefinition> picked)
    {
        if (globalPool == null || globalPool.rewards == null) return;
        if (picked == null) return;

        int need = rewardCount - picked.Count;
        if (need <= 0) return;

        var candidates = BuildCandidates(round, globalPool.rewards);

        // 중복 방지: 이미 뽑힌 것은 후보에서 제거
        candidates.RemoveAll(r => r == null || picked.Contains(r));

        while (picked.Count < rewardCount && candidates.Count > 0)
        {
            ItemRarity rarity = RollRarity_Global();

            RewardDefinition item = PickOneByRarity(candidates, rarity);

            if (item == null)
            {
                item = PickOneByRarity(candidates, ItemRarity.Special)
                    ?? PickOneByRarity(candidates, ItemRarity.Common)
                    ?? PickOneByRarity(candidates, ItemRarity.Rare)
                    ?? PickOneByRarity(candidates, ItemRarity.Legendary)
                    ?? PickOneByRarity(candidates, ItemRarity.Mythic);
            }

            if (item == null) break;

            picked.Add(item);
            candidates.Remove(item);
        }
    }
}