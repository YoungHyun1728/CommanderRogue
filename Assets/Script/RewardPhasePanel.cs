using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RewardPhasePanel : MonoBehaviour
{
    [Header("UI Parents")]
    [SerializeField] private Transform shopItemsParent;
    [SerializeField] private Transform rewardItemsParent;

    [Header("Prefabs")]
    [SerializeField] private RewardCardView shopItemCard;
    [SerializeField] private RewardCardView rewardItemCard;

    [Header("아이템 설명 UI")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("보상 리롤 UI")]
    [SerializeField] private UnityEngine.UI.Button rerollButton;
    [SerializeField] private TextMeshProUGUI rerollCostText;

    [Header("보상 페이즈 넘기기")]
    [SerializeField] private UnityEngine.UI.Button skipButton;

    private bool rewardTaken = false;

    private System.Action<RewardDefinition> onRewardSelected;
    private System.Action<RewardDefinition> onShopItemClicked;

    private System.Func<int> getRerollCost;
    private System.Action onReroll;
    private System.Action onSkip;

    private void Awake()
    {
        gameObject.SetActive(false);
        if (descriptionText != null)
            descriptionText.text = "";
    }

    public void Open(
        List<RewardDefinition> rewardChoices,
        List<RewardDefinition> shopChoices,
        System.Action<RewardDefinition> onRewardSelected,
        System.Action<RewardDefinition> onShopItemClicked,
        System.Func<int> getRerollCost,
        System.Action onReroll,
        System.Action onSkip)
    {
        rewardChoices ??= new List<RewardDefinition>();
        shopChoices   ??= new List<RewardDefinition>();

        bool hasFreeRewards = rewardChoices.Count > 0;

        rewardTaken = false;

        this.onRewardSelected  = onRewardSelected;
        this.onShopItemClicked = onShopItemClicked;
        this.getRerollCost = getRerollCost;
        this.onReroll = onReroll;
        this.onSkip = onSkip;

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(() => this.onReroll?.Invoke());
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => this.onSkip?.Invoke());
        }
        
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();

            // 무료 보상 카드가 있을 때만 리롤 활성
            rerollButton.gameObject.SetActive(hasFreeRewards);
            if (rerollCostText != null) rerollCostText.gameObject.SetActive(hasFreeRewards);

            if (hasFreeRewards)
            {
                rerollButton.interactable = true;
                rerollButton.onClick.AddListener(() => this.onReroll?.Invoke());
                RefreshRerollCostUI();
            }
        }

        ClearChildren(shopItemsParent);
        ClearChildren(rewardItemsParent);

        //상점
        foreach (var r in shopChoices)
        {
            var card = Instantiate(shopItemCard, shopItemsParent);
            card.Setup(
                r,
                true,
                OnClickShopItem,
                OnHoverCard,
                ClearDescription
            );
        }

        //라운드 보상
        foreach (var r in rewardChoices)
        {
            var card = Instantiate(rewardItemCard, rewardItemsParent);
            card.Setup(
                r,
                false,
                OnClickReward,
                OnHoverCard,
                ClearDescription
            );
        }

        if (descriptionText != null)
            descriptionText.text = "";
        
    }

    private void OnHoverCard(RewardDefinition reward)
    {
        if (descriptionText == null || reward == null) return;

        string desc = reward.description ?? "";

        // 플레이스홀더
        if (desc.Contains("{AMOUNT}"))
        {
            int amount = reward.goldAmount; // 기본값 fallback

            // 스케일링 반영된값
            if (RunManager.Instance != null && reward.rewardType == RewardType.Gold)
                amount = RunManager.Instance.GetGoldAmount(reward);

            desc = desc.Replace("{AMOUNT}", amount.ToString());
        }

        if (desc.Contains("{LEVELINCR}"))
        {
            int levelIncr = reward.levelIncrease; // 기본값 fallback

            // 스케일링 반영된값
            if (RunManager.Instance != null && reward.rewardType == RewardType.InstantExp)
                levelIncr = reward.levelIncrease + RunManager.Instance.levelPotionBonus;

            desc = desc.Replace("{LEVELINCR}", levelIncr.ToString());
        }

        descriptionText.text = desc;
    }

    private void ClearDescription()
    {
        if (descriptionText == null) return;
        descriptionText.text = "";
    }

    private void OnClickReward(RewardDefinition reward)
    {
        onRewardSelected?.Invoke(reward);
    }

    private void OnClickShopItem(RewardDefinition reward)
    {
       onShopItemClicked?.Invoke(reward);
    }
    
    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    public void RefreshRerollCostUI()
    {
        if (rerollCostText != null && getRerollCost != null)
            rerollCostText.text = $"{getRerollCost()} G";
    }

}
