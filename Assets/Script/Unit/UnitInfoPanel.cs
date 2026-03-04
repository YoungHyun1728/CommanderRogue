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
    [SerializeField] private EquipmentListTooltipTrigger equipmentTooltipTrigger;


    private Unit _target;
    

    public void Bind(Unit unit)
    {
        _target = unit;
        PrewarmTooltips();
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
        manaSkillIcon.color = (skill != null && skill.icon != null)
            ? new Color(1, 1, 1, 1f)   // 보임
            : new Color(1, 1, 1, 0f);  // 안 보이지만 레이캐스트는 유지
        
        // 스킬 툴팁
        skillTooltipTrigger?.SetContext(u, skill);

        // 장비
        equipmentTooltipTrigger?.SetContext(u);
        
        // 스탯 아이콘
        SetAlpha(strIconObj, u.MainStatType == UnitData.MainStat.strength ? 1f : 0f);
        SetAlpha(agiIconObj, u.MainStatType == UnitData.MainStat.agility ? 1f : 0f);
        SetAlpha(intIconObj, u.MainStatType == UnitData.MainStat.intelligence ? 1f : 0f);

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

    void SetAlpha(GameObject go, float a)
    {
        if (!go) return;
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = a;
        cg.blocksRaycasts = true;   // 항상 레이캐스트 가능
        cg.interactable = true;
    }

    private void PrewarmTooltips()
    {
        var sys = TooltipSystem.Instance ?? FindObjectOfType<TooltipSystem>();
        if (sys == null) return;

        // 빈 내용으로 한 번 켰다 바로 끄기
        sys.Show(TooltipChannel.UnitSkill, "", "", "");
        sys.Show(TooltipChannel.Stat, "", "", "");
        sys.Hide(TooltipChannel.UnitSkill);
        sys.Hide(TooltipChannel.Stat);
    }
}