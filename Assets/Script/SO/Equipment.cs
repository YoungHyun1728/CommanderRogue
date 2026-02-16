using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Equipment", menuName = "Game/Equipment")]
public class Equipment : ScriptableObject
{
    public string itemName;
    public Sprite icon;

    [Header("패시브 스킬")]
    public List<SkillDefinition> grantedPassives = new();
    
    // 증가하는 스탯들(고정값)
    public double bonusStrength;
    public double bonusAgility;
    public double bonusIntelligence;

    // 증가하는 스탯들(비율)
    public double bonusStrengthRate;
    public double bonusAgilityRate;
    public double bonusIntelligenceRate;

    // 그외 수치들
    public double baseMaxHp;
    public double bonusAttack;
}

// 필요한 추가스탯 수정시 Unit.cs의 장착, 탈착함수도 같이 수정 (추가변수들 선언필요)
