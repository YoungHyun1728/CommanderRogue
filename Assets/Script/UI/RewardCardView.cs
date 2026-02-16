using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class RewardCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText; // 상점용일 때만 사용
    [SerializeField] private Button button;

    [Header("Rarity Colors")]
    [SerializeField] private Color commonText;
    [SerializeField] private Color specialText;
    [SerializeField] private Color rareText;
    [SerializeField] private Color legendaryText;
    [SerializeField] private Color mythicText;

    private RewardDefinition reward;
    private Action<RewardDefinition> onClick;
    private Action<RewardDefinition> onHover;
    private Action onHoverExit;

    public void Setup(
        RewardDefinition reward,
        bool isShopItem,
        Action<RewardDefinition> onClick,
        Action<RewardDefinition> onHover,
        Action onHoverExit)
    {
        this.reward = reward;
        this.onClick = onClick;
        this.onHover = onHover;
        this.onHoverExit = onHoverExit;

        if (iconImage != null)
            iconImage.sprite = reward.icon;

        if (nameText != null)
            nameText.text = reward.rewardName;

        ApplyRarity(reward);

        if (priceText != null)
        {
            if (isShopItem)
            {
                priceText.gameObject.SetActive(true);
                priceText.text = RunManager.Instance.GetShopPrice(reward).ToString() + " G";
            }
            else
            {
                priceText.gameObject.SetActive(false);
            }
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                this.onClick?.Invoke(this.reward);
            });
        }

    }

    private void ApplyRarity(RewardDefinition r)
    {
        if (r == null) return;

        Color tx = r.rarity switch
        {
            ItemRarity.Special   => specialText,
            ItemRarity.Rare      => rareText,
            ItemRarity.Legendary => legendaryText,
            ItemRarity.Mythic    => mythicText,
            _                    => commonText,
        };
        
        if (nameText != null) nameText.color = tx;
    }

    //마우스포인터 올렸을때 아이템 설명
    public void OnPointerEnter(PointerEventData eventData)
    {
        onHover?.Invoke(reward);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onHoverExit?.Invoke();
    }
}
