using UnityEngine;

public enum RewardType
{
    Gold,             // 골드 증가
    Equipment,        // 캐릭터한테 장착할 장비
    InstantHeal,      // 즉시 회복
    InstantExp,       // 즉시 경험치
    PassiveItem,      // 패시브아이템
    WeatherChange,    // 날씨 변경
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
    public RewardTargetType targetType; // 타겟이 필요하면 캐릭터리스트UI 열기위해서 

    [TextArea] public string description;   // 카드 아래에 보여줄 설명

    [Header("등장 조건 / 밸런스 관련")]
    public bool canAppearAsReward = true;   // 라운드 보상 후보인지
    public bool canAppearInShop = true;     // 상점에 뜰 수 있는지
    public int minRound = 1;               // 몇 라운드 이상부터 나오는지
    public int maxRound = 999;             // 몇 라운드까지 나오는지
    //public float weight = 1f;              // 등장 확률 가중치 (나중에 쓰기 좋음)

    [Header("골드 관련")]
    public int goldAmount;    // rewardType == Gold 일 때 사용
    public int shopPrice;     // 상점에서 구매 비용

    [Header("장비 관련")]
    public Equipment equipment;   // rewardType == Equipment 일 때 사용

    [Header("포션 관련")]
    public double healAmount;        // rewardType == Potion (HP 회복량)
    public bool fullHeal;         // 전부 회복하는 포션인지

    [Header("EXP 포션 관련")]
    public double expAmount;         // rewardType == ExpPotion 일 때 사용

    [Header("패시브 아이템 (전투 중 소비)")]
    public int passiveStackAmount = 1;

    [Header("상태 변경")] 
    public WeatherType weatherType; // 날씨 변경
    // 나침반 추가 예정
}
