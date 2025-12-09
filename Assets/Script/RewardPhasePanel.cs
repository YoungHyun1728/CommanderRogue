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

    private bool rewardTaken = false;

    private System.Action<RewardDefinition> onRewardSelected;
    private System.Action<RewardDefinition> onShopItemClicked;

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
        System.Action<RewardDefinition> onShopItemClicked)
    {
        rewardTaken = false;

        this.onRewardSelected  = onRewardSelected;
        this.onShopItemClicked = onShopItemClicked;

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
        
        gameObject.SetActive(true);
    }

    private void OnHoverCard(RewardDefinition reward)
    {
        if (descriptionText == null) return;
        descriptionText.text = reward.description;
    }

    private void ClearDescription()
    {
        if (descriptionText == null) return;
        descriptionText.text = "";
    }

    private void OnClickReward(RewardDefinition reward)
    {
        if (rewardTaken) return;
        rewardTaken = true;

        onRewardSelected?.Invoke(reward);

        gameObject.SetActive(false);
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



}
