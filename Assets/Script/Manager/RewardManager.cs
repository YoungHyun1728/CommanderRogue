using System.Collections.Generic;
using UnityEngine;

public class RewardManager : MonoBehaviour
{
    [SerializeField] private List<RewardDefinition> allRewards;

    //보상에 들어갈 랜덤아이템리스트
    public List<RewardDefinition> GetRewardChoices(int round, int rewardCount)
    {
        var pool = new List<RewardDefinition>();

        foreach(var r in allRewards)
        {
            if(!r.canAppearAsReward) continue;
            if(round < r.minRound || round > r.maxRound) continue;

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
