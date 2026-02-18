using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Equipment", menuName = "Game/Equipment")]
public class Equipment : ScriptableObject
{
    public string itemName;
    public Sprite icon;

    [Header("패시브 스킬")]
    public List<SkillDefinition> grantedPassives = new();
    
    [Header("스탯 고정 수치 증가")]
    public double bonusStrength;
    public double bonusAgility;
    public double bonusIntelligence;

    [Header("스탯 비율증가 0.1 당 10% 증가")]
    public double bonusStrengthRate;
    public double bonusAgilityRate;
    public double bonusIntelligenceRate;

    [Header("파생수치")]
    public double baseMaxHp;
    public double bonusAttack;    
    public float hpRecovery;
    public float mpRecovery;
    public float criticalProbability;    
    public float criticalDamage;

    [Header("마이너스가 좋은 스탯")]
    public float attackInretval;
    public float maxMp;
    
    [Header("공격 사거리")]
    public int attackRange;
}

// 필요한 추가스탯 수정시 Unit.cs의 장착, 탈착함수도 같이 수정 (추가변수들 선언필요)
