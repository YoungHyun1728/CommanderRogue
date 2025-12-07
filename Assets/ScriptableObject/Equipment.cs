using UnityEngine;

[CreateAssetMenu(fileName = "Equipment", menuName = "Game/Equipment")]
public class Equipment : ScriptableObject
{
    public string itemName;
    public Sprite icon;

    // 증가하는 스탯들
    public double bonusStrength;
    public double bonusAgility;
    public double bonusIntelligence;

    // 추가로 필요하면
    public double baseMaxHp;
    public double bonusAttack;
}

// 필요한 추가스탯 수정시 Unit.cs의 장착, 탈착함수도 같이 수정해야함
