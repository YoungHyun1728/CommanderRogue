using UnityEngine;

/// <summary>
/// 유닛의 기본 정보와 스탯, 레벨업, 장비 관련
/// 
/// </summary>
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
    public double exp = 0;
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

    //힘스탯 파생 수치
    public double bonusmaxhp;
    public float hpRecovery;
    //민첩스탯 파생 수치
    public float bonusattackInretval;
    public float bonusCriticalProbability; // 치명타 확률증가량
    //지능 스탯 파생 수치
    public float bonusExp;
    public float mpRecovery = 10.0f; // 기본 마나 회복량

    //전투 관련 데이터
    public double baseAttackDamage = 10.0f; // 기본 공격력
    public double attackDamage; // 최종 공격력
    public float attackInretval = 1.0f;
    public int attackRange = 1;
    public float criticalDamage = 1.4f;
    public float criticalProbability; //치명타 확률

    //HUD 데이터
    public float basemaxHp = 100; // 기본 최대체력, 고정수치아이템으로 증가시켜서 사용
    public double maxHp;
    public double hp = 100;
    public float maxMp = 100;
    public float mp = 0;
    public double maxShield;
    public double shield;

    //장비 관련 데이터 추가예정

    void Awake()
    {
        UpdateAllStats();
    }

    // 능력치 업데이트
    void UpdateAllStats()
    {
        UpdateTotalStats();
        UpdateBonusStats();
    }

    // 총스탯 계산, 주스탯에 따라 공격력 증가
    void UpdateTotalStats()
    {
        totalStrength = strength + bonusStrength;
        totalAgility = agility + bonusAgility;
        totalIntelligence = intelligence + bonusIntelligence;

        switch(mainStat)
        {
            case MainStat.strength:
                attackDamage = baseAttackDamage + totalStrength;
                break;
            case MainStat.agility:
                attackDamage = baseAttackDamage + totalAgility;
                break;
            case MainStat.intelligence:
                attackDamage = baseAttackDamage + totalIntelligence;
                break;
        }
    }
    
    void UpdateBonusStats()
    {
        bonusmaxhp = totalStrength * 10;  // 1당 최대체력 10 증가
        hpRecovery = (float)totalIntelligence * 0.25f;  // 100당 체력회복량 25 증가
        bonusattackInretval = (float)totalAgility * 0.005f;  // 100당 공격딜레이 0.5초 감소
        bonusCriticalProbability = (float)totalAgility * 0.1f; // 100당 치명타 확률 10% 증가
        bonusExp = (float)totalIntelligence * 0.1f; // 100당 경험치 획득량 10% 증가
        mpRecovery = (float)totalIntelligence * 0.05f; // 100당 마나회복량 5 증가
        
        //주스탯 보너스
        if(mainStat == MainStat.strength)
        {
            bonusmaxhp = totalStrength * 20;
            hpRecovery = (float)totalStrength * 0.05f;
        }

        if(mainStat == MainStat.agility)
        {
            bonusattackInretval = (float)totalAgility * 0.01f;
            bonusCriticalProbability = (float)totalAgility * 0.01f;
        }

        if(mainStat == MainStat.intelligence)
        {
            bonusExp = (float)totalIntelligence * 0.2f;
            mpRecovery = (float)totalIntelligence * 0.1f;
        }
        // 공격 딜레이는 최소 0.2초
        attackInretval = Mathf.Max(0.2f, 1.0f - bonusattackInretval);
        criticalProbability = Mathf.Min(100.0f, bonusCriticalProbability);

        maxHp = basemaxHp + bonusmaxhp;
        hp += bonusmaxhp;
    }

    public void HpRegen(float deltaTime)
    {
        double addHp = (double)(hpRecovery * deltaTime);
        hp = System.Math.Min(hp + addHp, maxHp);
    }

    public void DealDamage(Unit target)
    {
        float rand = Random.Range(0f, 100.0f);

        double damage;
        if (rand < criticalProbability)
        {
            Debug.Log("[Unit] 치명타 발생!");
            damage = attackDamage * criticalDamage;
        }
        else
        {
            damage = attackDamage;
        }

        target.hp -= damage;

        // 마나 회복
        mp = Mathf.Min(mp + mpRecovery, maxMp);
    }

    void LevelUp()
    {
        if(exp > levelUpExp[level] && level < maxLevel)
        {
            level++;
            strength += strengthPerLevel;
            agility += agilityPerLevel;
            intelligence += intelligencePerLevel;

            exp = exp - levelUpExp[level - 1];
            UpdateAllStats();
        }
    }

    void Equip()
    {
        //장비장착
        //장비 리스트에서 장비아이템 삭제후 캐릭터의 장비리스트에 추가
        //장비 장착에 따른 스탯 재계산 ex) bonusStrength += equipment.strength;
        UpdateAllStats();
    }

    public void UnEquip()
    {
        //장비 해체
        //캐릭터의 장비리스트에서 아이템 삭제 후 장비리스트에 추가
        //장비 해제에 따른 스탯 재계산 ex) bonusStrength -= equipment.strength;
        UpdateAllStats();
    }



}
