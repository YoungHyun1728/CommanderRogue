using UnityEngine;

public class Unit : MonoBehaviour
{
    public string unitName;
    private enum MainStat // 주 스탯
    {
        strength, agility, intelligence
    }

    private MainStat mainStat; // 유닛의 주 스탯

    // 레벨 관련 데이터
    public int level = 1;
    public int maxLevel = 200;
    public double exp;
    public double[] levelUpExp = new double[199];
    
    //기본 스탯
    public double strength;
    public double agility;
    public double intelligence;

    //레벨업당 스탯 증가량
    public float strengthPerLevel;
    public float agilityPerLevel;
    public float intelligencePerLevel;

    //추가 스탯 아이템이나 버프 등으로 인한 보너스 스탯
    public double bonusStrength;
    public double bonusAgility;
    public double bonusIntelligence;

    //최종 스탯 UI에 표시되고 전투에 반영되는 스탯
    public double totalStrength;
    public double totalAgility;
    public double totalIntelligence;

    //힘스탯 관련 수치
    public double bonusmaxhp;
    public float hpRecovery;
    //민첩스탯 관련 수치
    public float bonusattackInretval;
    public float bonusCriticalProbability; // 치명타 확률증가량
    //지능 스탯 관련 수치
    public float bonusExp;
    public float mpRecovery;

    //전투 관련 데이터
    public double attackDamage;
    public double attackInretval = 1.0;
    public double attackRange = 1.0;
    public float criticalDamage = 1.4f; 
    public double criticalProbability; // 치명타 확률

    //HUD 데이터
    public double maxHp = 100;
    public double hp = 100;
    public double maxMp = 100;
    public double mp;
    public double maxShield;
    public double shield;

    //장비 관련 데이터

    void Awake()
    {
        UpdateStats();
    }

    // 능력치 업데이트
    void UpdateStats()
    {
        totalStrength = strength + bonusStrength;
        totalAgility = agility + bonusAgility;
        totalIntelligence = intelligence + bonusIntelligence;
        maxHp = 100 + bonusmaxhp;

    }
    
    void LevelUp()
    {
        if(exp > levelUpExp[level] && level < maxLevel)
        {
            level++;
            strength += strengthPerLevel;
            agility += agilityPerLevel;
            intelligence += intelligencePerLevel;
        }
    }

    void Equip()
    {
        //장비장착
        //장비 리스트에서 장비아이템 삭제후 캐릭터의 장비리스트에 추가
    }

    public void UnEquip()
    {
        //장비 해체
        //캐릭터의 장비리스트에서 아이템 삭제 후 장비리스트에 추가
    }

}
