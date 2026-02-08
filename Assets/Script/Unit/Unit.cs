using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 유닛의 기본 정보와 스탯, 레벨업, 장비 관련
/// 
/// </summary>
public class Unit : MonoBehaviour
{
    public string unitName;
    public Sprite portrait;
    [SerializeField] private UnitSpawnSpeechDatabase speechDb;
    private enum MainStat // 주 스탯
    {
        strength, agility, intelligence
    }

    private MainStat mainStat; // 유닛의 주 스탯
    public List<Equipment> equippedItems = new List<Equipment>(); //장착중인 장비리스트

    // 레벨 관련 데이터
    public int level;
    public const int maxLevel = 200;
    public double exp = 0;
    public double[] levelUpExp = new double[maxLevel + 1];
    
    //기본 스탯
    public double strength;
    public double agility;
    public double intelligence;

    //레벨업당 스탯 증가량
    public float strengthPerLevel;
    public float agilityPerLevel;
    public float intelligencePerLevel;

    //추가 스탯 아이템이나 버프 등으로 인한 보너스 스탯(고정수치)
    public double bonusStrength;
    public double bonusAgility;
    public double bonusIntelligence;

    //추가 스탯 아이템이나 버프 등으로 인한 보너스 스탯(비율수치)
    public double bonusStrengthRate;
    public double bonusAgilityRate;
    public double bonusIntelligenceRate;

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
    public float bonusExp;           // 지능스탯에 따라 경험치 획득 효율증가
    public float mpRecovery;         //마나 회복량


    //전투 관련 데이터
    public double baseAttackDamage; // 기본 공격력
    public double bonusAttackDamage; // 장비로 추가된 공격력
    public double attackDamage; // 최종 공격력
    public float attackInretval = 1.5f;
    public int attackRange = 1;
    public float criticalDamage = 1.4f;
    public float criticalProbability; //치명타 확률
    

    //HUD 데이터
    public double baseMaxHp; // 기본 최대체력, 고정수치아이템으로 증가시켜서 사용
    public double maxHp; // 기본최대체력 + 증가된 최대체력을 반영
    public double hp; //현재체력
    public float maxMp = 100;
    public float mp;
    public float baseMpRecovery = 10.0f;
    public double maxShield;
    public double shield;

    // 상태이상 관련 데이터
    public float incomingDamageMultiplier = 1f; // 받는 피해량 배율 (버프/디버프용)
    public float moveSpeedMultiplier = 1f;      // 기본 1.0f, 이동속도 배율 (버프/디버프용)
    public float moveSpeed = 3.0f;            // 기본 이동속도

    //아이템 관련 데이터 추가예정
    //패시브아이템 소지수
    public int emergencyPotionCount; //전투 중 체력회복

    // 스킬 관련 데이터
    [HideInInspector] public float attackIntervalMultiplier = 1f; // 공격 딜레이 (버프/디버프용)
    public float EffectiveAttackInterval
    {
        get { return Mathf.Max(0.2f, attackInretval * attackIntervalMultiplier); }
    }

    void Awake()
    {
        UpdateAllStats();
    }

    void Start()
    {
        if (speechDb == null) return;

        string line = speechDb.GetLine(unitName);
        if (!string.IsNullOrEmpty(line))
        {
            FloatingTextPoolManager.Instance.ShowSpeech(
                transform, line, new Vector3(0f, 1.3f, 0f)
            );
        }
    }

    // 능력치 업데이트
    void UpdateAllStats()
    {
        double oldMax = maxHp;

        UpdateTotalStats();
        UpdateBonusStats();

        double delta = maxHp - oldMax;
        hp += delta; // 최대체력 증가량만큼 현재체력도 증가

        // 안전 클램프
        if (hp > maxHp) hp = maxHp;
        if (hp < 0) hp = 0;
    }

    // 총스탯 계산, 주스탯에 따라 공격력 증가
    void UpdateTotalStats()
    {
        // 순수스탯 (기본 + 레벨업)
        double pureStrength     = strength     + level * strengthPerLevel;
        double pureAgility      = agility      + level * agilityPerLevel;
        double pureIntelligence = intelligence + level * intelligencePerLevel;

        // 최종 반영스탯
        totalStrength     = pureStrength     * (1.0 + bonusStrengthRate)     + bonusStrength;
        totalAgility      = pureAgility      * (1.0 + bonusAgilityRate)      + bonusAgility;
        totalIntelligence = pureIntelligence * (1.0 + bonusIntelligenceRate) + bonusIntelligence;

        switch(mainStat)
        {
            case MainStat.strength:
                attackDamage = baseAttackDamage + bonusAttackDamage + totalStrength;
                break;
            case MainStat.agility:
                attackDamage = baseAttackDamage + bonusAttackDamage + totalAgility;
                break;
            case MainStat.intelligence:
                attackDamage = baseAttackDamage + bonusAttackDamage + totalIntelligence;
                break;
        }
    }
    
    void UpdateBonusStats()
    {
        bonusmaxhp = totalStrength * 10;  // 1당 최대체력 10 증가
        hpRecovery = (float)totalIntelligence * 0.25f;  // 100당 체력회복량 25 증가
        bonusattackInretval = (float)totalAgility * 0.0025f;  // 100당 공격딜레이 0.25초 감소
        bonusCriticalProbability = (float)totalAgility * 0.1f; // 100당 치명타 확률 10% 증가
        bonusExp = (float)totalIntelligence * 0.1f; // 100당 경험치 획득량 10% 증가
        mpRecovery = baseMpRecovery + (float)totalIntelligence * 0.05f; // 100당 마나회복량 5 증가
        
        //주스탯 보너스 파생스탯증가량 상승
        if(mainStat == MainStat.strength)
        {
            bonusmaxhp = totalStrength * 20;
            hpRecovery = (float)totalStrength * 0.05f;
        }

        if(mainStat == MainStat.agility)
        {
            bonusattackInretval = (float)totalAgility * 0.005f;
            bonusCriticalProbability = (float)totalAgility * 0.01f;
        }

        if(mainStat == MainStat.intelligence)
        {
            bonusExp = (float)totalIntelligence * 0.2f;
            mpRecovery = baseMpRecovery + (float)totalIntelligence * 0.1f;
        }
        // 공격 딜레이는 최소 0.2초
        attackInretval = Mathf.Max(0.2f, 1.0f - bonusattackInretval);
        criticalProbability = Mathf.Min(100.0f, bonusCriticalProbability);

        maxHp = baseMaxHp + bonusmaxhp;
    }
    
    public double GetMainStatTotal()
    {
        // mainStat totalstat getter
        var t = typeof(Unit).GetField("mainStat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var v = (int)t.GetValue(this);

        // enum 순서: strength=0, agility=1, intelligence=2 (Unit.cs 선언 기준)
        if (v == 0) return totalStrength;
        if (v == 1) return totalAgility;
        return totalIntelligence;
    }

    public void RefreshStats()
    {
        // 기존 private UpdateAllStats()를 public wrapper로 호출
        // Unit.cs에 UpdateAllStats()가 있으니 그걸 그대로 호출하면 됨.
        var m = typeof(Unit).GetMethod("UpdateAllStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        m.Invoke(this, null);
    }

    public void GainExp(double amount)
    {
        double gained = amount * (1.0 + bonusExp);

        exp += gained;

        // 레벨업 루프
        while (level < maxLevel)
        {
            double need = RunManager.Instance.GetRequiredExp(level);
            if (exp < need) break;

            exp -= need;
            level++;
            strength += strengthPerLevel;
            agility += agilityPerLevel;
            intelligence += intelligencePerLevel;
        }

        UpdateAllStats();
        Debug.Log($"{unitName} 경험치 +{amount}");
    }
    
    //아이템으로 인한 레벨업
    public void GainLevel(int amount)
    {
        for (int i = 0; i < amount && level < maxLevel; i++)
        {
            level++;
        }

        UpdateAllStats();
    }

    void LevelUp()
    {
        if(exp >= levelUpExp[level] && level < maxLevel)
        {
            exp -= levelUpExp[level];
            level++;
            UpdateAllStats();
        }
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
        
        // 강화공격 보너스 적용
        var skills = GetComponent<UnitSkillSystem>();
        if (skills != null)
            damage += skills.ConsumeEnhancedBonusDamage(target);
        
        target.ReceiveDamage(damage, this);
        mp = Mathf.Min(mp + mpRecovery, maxMp);


        // OnHit 패시브 트리거(근접 공격은 여기서 hit 확정)
        if (skills != null)
            skills.NotifyBasicAttackHit(target.gameObject);
    }

    public void ReceiveDamage(double amount, Unit attacker)
    {
        amount *= incomingDamageMultiplier;

        // 쉴드 먼저 소모
        if (shield > 0)
        {
            double used = System.Math.Min(shield, amount);
            shield -= used;
            amount -= used;

            if (shield <= 0)
            {
                shield = 0;
                maxShield = 0;
            }
        }

        if (amount > 0)
            TakeDamage(amount);

        // 피격 패시브 트리거
        var skills = GetComponent<UnitSkillSystem>();
        if (skills != null)
            skills.NotifyTakeDamage(attacker != null ? attacker.gameObject : null);
    }

    public void TakeDamage(double amount)
    {
        hp -= amount;
        FloatingTextPoolManager.Instance.ShowDamage(
            transform, (int)amount, new Vector3(0, 1.3f, 0)
        );
        if (hp < 0) hp = 0;

        // 비상포션 사용
        if (hp <= maxHp / 2 && emergencyPotionCount > 0)
        {
            emergencyPotionCount--;
            double heal = maxHp / 4;
            Heal(heal);
            //Debug.Log($"{unitName} 비상포션 발동! HP {heal} 회복, 남은 개수: {emergencyPotionCount}");
        }
    }

    public void HealByPotion(double fixedAmount, float proportion, bool fullHeal)
    {
        double healValue = 0;

        if (fullHeal)
        {
            healValue = maxHp; // Heal()에서 maxHp를 넘지 않도록 처리하니 괜찮음
        }
        else
        {
            //포션의 고정값 vs 비율값. 어느쪽이 더큰지 결정
            double byFixed    = fixedAmount;
            double byPercent  = proportion > 0 ? maxHp * proportion : 0;
            healValue         = System.Math.Max(byFixed, byPercent);
        }

        Heal(healValue);
    }

    public void Heal(double amount)
    {
        hp += amount;
        FloatingTextPoolManager.Instance.ShowHeal(
            transform, (int)amount, new Vector3(0, 1.3f, 0)
        );
        if (hp > maxHp) hp = maxHp; //최대값넘는거 금지
        
    }

    public void Equip(Equipment eq)
    {
        //장비장착
        equippedItems.Add(eq);

        //고정수치 증가량
        bonusStrength += eq.bonusStrength;
        bonusAgility += eq.bonusAgility;
        bonusIntelligence += eq.bonusIntelligence;

        //비율수치 증가량
        bonusStrengthRate     += eq.bonusStrengthRate;   // 0.2f 같은 값
        bonusAgilityRate      += eq.bonusAgilityRate;
        bonusIntelligenceRate += eq.bonusIntelligenceRate;
        
        //장비가 주는 체력 증가량
        baseMaxHp += eq.baseMaxHp;

        UpdateAllStats();
    }

    public void UnEquip(Equipment eq)
    {
        //장비 해제
        equippedItems.Remove(eq);

        //고정수치 감소
        bonusStrength -= eq.bonusStrength;
        bonusAgility -= eq.bonusAgility;
        bonusIntelligence -= eq.bonusIntelligence;

        //비율수치 감소
        bonusStrengthRate     -= eq.bonusStrengthRate;   
        bonusAgilityRate      -= eq.bonusAgilityRate;
        bonusIntelligenceRate -= eq.bonusIntelligenceRate;
        
        //장비가 주는 체력 감소
        baseMaxHp -= eq.baseMaxHp;

        UpdateAllStats();
    }

    public void ApplyData(UnitData data)
    {
        portrait = data.portrait;
        unitName = data.unitName;
        level = data.level;

        baseMaxHp = data.baseMaxHp;
        baseAttackDamage = data.baseAttackDamage;

        strength = data.strength;
        agility = data.agility;
        intelligence = data.intelligence;
        attackRange = data.attackRange;

        strengthPerLevel = data.strengthPerLevel;
        agilityPerLevel = data.agilityPerLevel;
        intelligencePerLevel = data.intelligencePerLevel;

        // 메인 스탯 매핑
        switch (data.mainStat)
        {
            case UnitData.MainStat.strength: mainStat = MainStat.strength; break;
            case UnitData.MainStat.agility: mainStat = MainStat.agility; break;
            case UnitData.MainStat.intelligence: mainStat = MainStat.intelligence; break;
        }

        // HP/MP 초기화 및 스탯 재계산
        hp = baseMaxHp;
        maxHp = baseMaxHp;
        mp = 0f;

        UpdateAllStats();
    }

    public void AddPassiveItem(RewardDefinition reward)
    {
        // 타입별로 나누고 싶으면 reward안에 PassiveItemType 같은 enum을 또 둘 수도 있음
        // 일단 예시로 emergencyPotionCount만
        emergencyPotionCount += reward.passiveStackAmount; // 비상포션 
        Debug.Log($"{unitName} 에게 비상포션 {reward.passiveStackAmount}개 지급. 총: {emergencyPotionCount}");
    }   

}
