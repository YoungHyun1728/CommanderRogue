using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("기본 정보")]
    public string unitName;
    public Sprite portrait;
    public Sprite uiPortrait;
    [TextArea] public string unitSummary;

    [Header("프리팹")]
    public GameObject prefab;

    [Header("기본 능력치")]
    public float baseMaxHp = 100;
    public double baseAttackDamage = 10;

    public enum MainStat
    {
        strength, agility, intelligence
    }
    public MainStat mainStat;

    // 기본 스탯
    public double strength;
    public double agility;
    public double intelligence;
    public int attackRange = 1;
    public float attackSpeed = 0.8f;

    [Header("레벨업당 증가치")]
    public float strengthPerLevel;
    public float agilityPerLevel;
    public float intelligencePerLevel;

    [Header("레벨 정보")]
    public int level;

    [Header("초기 스킬")]
    public SkillDefinition fullManaSkill;                  // 풀마나 액티브
    [TextArea] public string fullManaSkillDescription;     // 선택 패널용 설명
    public List<SkillDefinition> startingPassives = new(); // 패시브 스킬들

    [Header("초기 장비")]
    public List<Equipment> startingEquipments = new();    // 처음 지급 장비
}
