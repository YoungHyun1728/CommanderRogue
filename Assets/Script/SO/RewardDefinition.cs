using System.Diagnostics.Contracts;
using UnityEngine;

public enum RewardType
{
    Gold,             // 골드 증가
    Equipment,        // 캐릭터한테 장착할 장비
    InstantHeal,      // 즉시 회복
    InstantExp,       // 즉시 경험치
    PassiveItem,      // 패시브아이템(전투중 소비형 아이템)
    WeatherChange,    // 날씨 변경
    Relic,             // 유물(RunMnager 내에서 변수로 제어되는 아이템)
    Revive,
    GainUnit
}

public enum ItemRarity
{
    Common,     // 흰색 54.12%
    Special,    // 파랑 33%
    Rare,       // 보라 15.5%
    Legendary,  // 빨강 4.5%
    Mythic      // 민트 1.12%
}

public enum RewardTargetType
{
    None,        // 유닛 선택 필요 없는 아이템
    ChooseUnit,  // 플레이어가 유닛 하나 선택
    RandomUnit,  // 랜덤 유닛에게 적용
}

[CreateAssetMenu(fileName = "Reward", menuName = "Game/Reward Definition")]
public class RewardDefinition : ScriptableObject
{

    public string rewardName;
    public Sprite icon;
    public RewardType rewardType;       // 아이템의 타입 (골드, 장비, 전투중 소비 등등)
    
    [Header("등급")]
    public ItemRarity rarity = ItemRarity.Common;
    public RewardTargetType targetType; // 타겟이 필요하면 캐릭터리스트UI 열기위해서 

    [TextArea] public string description;   // 카드 아래에 보여줄 설명

    [Header("등장 조건 / 밸런스 관련")]
    public bool canAppearAsReward = true;   // 라운드 보상 후보인지
    public bool canAppearInShop = true;     // 상점에 파는 아이템인지
    public int minRound = 1;               // 몇 라운드 이상부터 나오는지
    public int maxRound = 999;             // 몇 라운드까지 나오는지
    //public float weight = 1f;              // 등장 확률 가중치 (나중에 쓰기 좋음)

    [Header("장비 관련")]
    public Equipment equipment;   // rewardType == Equipment 일 때 사용

    [Header("포션 관련")] // 고정된 회복량과 최대체력의 비율 둘중에 높은쪽으로 회복
    public double healAmount;        // rewardType == Potion (HP 회복량)
    public float healProportion;    // 최대체력의 비율로 회복
    public bool fullHeal;         // 전부 회복하는 포션인지

    [Header("골드 관련")]
    public int goldAmount;    // rewardType == Gold 일 때 사용
    public int shopPrice;     // 상점에서 구매 비용
    public bool scaleWithRound = true;     // 대부분 아이템: true
    public int baseShopPrice = 500;        // 라운드 1 기준 가격
    public float roundPriceMultiplier = 1.04f; // 예: 라운드마다 4% 증가

    [Header("EXP 포션 관련")]
    public int levelIncrease;         // rewardType == ExpPotion 일 때 사용

    [Header("패시브 아이템 (전투 중 소비)")]
    public int passiveStackAmount = 1;

    [Header("날씨 변경")] 
    public WeatherType weatherType; // 날씨 변경
    
    [Header("유물")]
    public int levelPotionBonus = 1; // 경험의서 (경험비약의 효율을 1씩 올려줌)
    public int expAmulet = 1;        // 경험부적 (경험치 획득 효율 증가 1개당 25%)
    public int goldAmulet = 1;       // 부적금화 (골드 획득 효율 증가 1개당 25%)

    [Header("부활")]
    public bool reviveHerb;         // 부활초 한명 반피로 부활
    public bool revivePotion;       // 부활포션 한명 풀피로 부활
    public bool reviveAsh;          // 부활초분말 모두 부활 모두 회복

    [Header("파티모집")]    
    public bool scaleWithPurchaseCount = false; // 소환서만 true
    public int pricePerPurchase = 1000;         // 구매할 때마다 추가    
}

