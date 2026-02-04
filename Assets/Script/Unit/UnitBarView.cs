using UnityEngine;
using UnityEngine.UI;

public class UnitBarView : MonoBehaviour
{
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider mpBar;
    [SerializeField] private Slider shieldBar; // 나중에 연결

    private Unit unit;

    public void Bind(Unit u)
    {
        unit = u;
        Refresh();
    }

    public void Refresh()
    {
        if (unit == null) return;

        if (hpBar != null && unit.maxHp > 0f)
            hpBar.value = Mathf.Clamp01((float)(unit.hp / unit.maxHp)); // UnitHUD랑 동일 계산 

        if (mpBar != null && unit.maxMp > 0f)
            mpBar.value = Mathf.Clamp01(unit.mp / unit.maxMp);          // UnitHUD랑 동일 계산

        if (shieldBar != null && unit.maxShield > 0f)
            shieldBar.value = Mathf.Clamp01((float)(unit.shield / unit.maxShield)); // UnitHUD랑 동일 계산
    }
}
