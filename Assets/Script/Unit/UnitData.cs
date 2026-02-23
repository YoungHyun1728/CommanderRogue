using UnityEngine;

[CreateAssetMenu(menuName = "Game/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("표시용 정보")]
    public string unitName;     // 이름
    public Sprite portrait;     // 이미지
    public string unitSummary;   // 유닛 설명(요약)

    [Header("프리팹")]
    public GameObject prefab;

    [Header("기본 스탯")]
    public float baseMaxHp = 100;
    public double baseAttackDamage = 10;

    public enum MainStat // 주 스탯
    {
        strength, agility, intelligence
    }
    public MainStat mainStat; // 유닛의 주 스탯
    //기본스탯
    public double strength;
    public double agility;
    public double intelligence;
    public int attackRange = 1;
    public float attackSpeed = 0.8f;

    //레벨업당 증가스탯
    public float strengthPerLevel;
    public float agilityPerLevel;
    public float intelligencePerLevel;



    [Header("레벨 관련")]
    public int level;
}
