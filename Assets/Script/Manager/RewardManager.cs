using System.Collections.Generic;
using System.Security;
using UnityEngine;

public class RewardManager : MonoBehaviour
{
    [SerializeField] private List<RewardDefinition> allRewards;
    [SerializeField] private List<RewardDefinition> questRewards;
    [SerializeField] private List<RewardDefinition> bossRewards;
    [SerializeField] private List<RewardDefinition> banditEncounterRewards;    
    [SerializeField] private List<EventRewardPool> eventRewardPools;

    [System.Serializable]
    public class EventRewardPool
    {
        public string eventId;
        public List<RewardDefinition> rewards;
    }

    //보상에 들어갈 랜덤아이템리스트
    public List<RewardDefinition> GetRewardChoices(
        int round, int rewardCount,
        NodeType nodeType, string eventId,
        bool forceGlobalPool = false)
    {
        List<RewardDefinition> source = allRewards;

        if (!forceGlobalPool)
        {
            // 이벤트키가 있으면 이벤트 풀 우선
            if (!string.IsNullOrEmpty(eventId))
            {
                var match = eventRewardPools.Find(p => p != null && p.eventId == eventId);
                if (match != null && match.rewards != null && match.rewards.Count > 0)
                    source = match.rewards;
            }
            else
            {
                // 노드 타입 기반
                if (nodeType == NodeType.Boss) source = bossRewards;
                else if (nodeType == NodeType.Event ) source = questRewards;
                else source = allRewards; 
            }
        }

        var pool = new List<RewardDefinition>();
        foreach (var r in source)
        {
            if (r == null) continue;
            if (!r.canAppearAsReward) continue;
            if (round < r.minRound || round > r.maxRound) continue;
            pool.Add(r);
        }

        return TakeRandomDistinct(pool, rewardCount);
    }

    //상점에 들어갈 고정 아이템 리스트
    public List<RewardDefinition> GetShopItems(int round)
    {
        var result = new List<RewardDefinition>();

        foreach (var r in allRewards)
        {
            if (!r.canAppearInShop) continue;
            if (round < r.minRound || round > r.maxRound) continue;

            result.Add(r);
        }
        
        return result;
    }

    private List<RewardDefinition> TakeRandomDistinct(List<RewardDefinition> pool, int count)
    {
        var result = new List<RewardDefinition>();
        var temp = new List<RewardDefinition>(pool);

        for (int i = 0; i < count && temp.Count > 0; i++)
        {
            int idx = Random.Range(0, temp.Count);
            result.Add(temp[idx]);
            temp.RemoveAt(idx);
        }

        return result;
    }
    
}
