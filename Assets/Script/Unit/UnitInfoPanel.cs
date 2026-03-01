using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitInfoPanel : MonoBehaviour
{
    [Header("Top")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image manaSkillIcon;

    [Header("HP/MP")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text maxHpText;
    [SerializeField] private TMP_Text mpText;
    [SerializeField] private TMP_Text maxMpText;

    [Header("Stats")]
    [SerializeField] private TMP_Text strText;
    [SerializeField] private TMP_Text agiText;
    [SerializeField] private TMP_Text intText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text attackSpeedText;

    [Header("StatsIcon")]
    [SerializeField] private GameObject strIconObj;
    [SerializeField] private GameObject agiIconObj;
    [SerializeField] private GameObject intIconObj;

    [SerializeField] private SkillTooltipTrigger skillTooltipTrigger;

    private Unit _target;
    

    public void Bind(Unit unit)
    {
        _target = unit;
        Refresh();
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_target == null) return;

        Refresh();
    }

    private void Refresh()
    {
        var u = _target;
        if (u == null) return;

        if (portraitImage) portraitImage.sprite = u.portrait;
        if (nameText) nameText.text = u.unitName;
        if (levelText) levelText.text = $"LV. {u.level}";
        
        // 스킬 아이콘
        var skillSystem = u.GetComponent<UnitSkillSystem>();
        var skill = skillSystem != null ? skillSystem.FullManaActive : null;
        manaSkillIcon.sprite = skill != null ? skill.icon : null;
        manaSkillIcon.enabled = (skill != null && skill.icon != null);
        // 스킬 툴팁
        skillTooltipTrigger?.SetContext(u, skill);
        
        // 스탯 아이콘
        strIconObj.SetActive(u.MainStatType == UnitData.MainStat.strength);
        agiIconObj.SetActive(u.MainStatType == UnitData.MainStat.agility);
        intIconObj.SetActive(u.MainStatType == UnitData.MainStat.intelligence);

        if (hpText) hpText.text = $"{u.hp:N0}";
        if (mpText) mpText.text = $"{u.mp:N0}";
        if (maxHpText) maxHpText.text = $"{u.maxHp:N0}";
        if (maxMpText) maxMpText.text = $"{u.maxMp:N0}";

        if (strText) strText.text = ((int)u.totalStrength).ToString("N0");
        if (agiText) agiText.text = ((int)u.totalAgility).ToString("N0");
        if (intText) intText.text = ((int)u.totalIntelligence).ToString("N0");

        if (attackText) attackText.text = ((int)u.attackDamage).ToString("N0");
        if (rangeText) rangeText.text = $"{u.attackRange} 칸";
        if (attackSpeedText) attackSpeedText.text = $"{u.EffectiveAttackSpeed:0.0}/s";
    }
}