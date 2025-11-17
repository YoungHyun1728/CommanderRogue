using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UnitHUD : MonoBehaviour
{
    [SerializeField]
    private Unit unit;               // hp, mp 등의 변수를 가진 unit;
    public Slider hpBar;
    public Slider mpBar;

    void Awake()
    {
        if (unit == null) unit = GetComponentInParent<Unit>();
        InitBars();
        RefreshBars();
    }

    void LateUpdate()
    {
        RefreshBars();
    }

    public void RefreshBars()
    {
        RefreshHP();
        RefreshMP();
    }

    void InitBars()
    {
        if (hpBar != null)
        {
            hpBar.minValue = 0f;
            hpBar.maxValue = 1f;
        }

        if (mpBar != null)
        {
            mpBar.minValue = 0f;
            mpBar.maxValue = 1f;
        }
    }

    void RefreshHP()
    {
        if (hpBar == null || unit == null || unit.maxHp <= 0f) return;
        double ratio = unit.hp / unit.maxHp;
        hpBar.value = Mathf.Clamp01((float)ratio);
    }

    void RefreshMP()
    {
        if (mpBar == null || unit == null || unit.maxMp <= 0f) return;
        double ratio = unit.mp / unit.maxMp;
        mpBar.value = Mathf.Clamp01((float)ratio);
    }
    
    public void ResetForSpawn()
    {
        InitBars();
        RefreshBars();
        gameObject.SetActive(true);
    }
}
