using System;
using UnityEngine;

public enum MetaUpgradeType
{
    StartGold,
    BaseMaxHp,
    BaseAttackDamage,
    BaseAttackSpeed,
    Strength,
    Agility,
    Intelligence
}

[Serializable]
public class MetaProgressState
{
    public double unspentScore;
    public int startGoldLevel;
    public int baseMaxHpLevel;
    public int baseAttackDamageLevel;
    public int baseAttackSpeedLevel;
    public int strengthLevel;
    public int agilityLevel;
    public int intelligenceLevel;
}

[Serializable]
public struct UpgradeCostCurve
{
    public double baseCost;
    public double growth;
    public int maxLevel;

    public UpgradeCostCurve(double baseCost, double growth, int maxLevel)
    {
        this.baseCost = baseCost;
        this.growth = growth;
        this.maxLevel = maxLevel;
    }

    public double GetCost(int currentLevel)
    {
        int level = Mathf.Max(0, currentLevel);
        return Math.Round(baseCost * Math.Pow(growth, level));
    }
}

public struct MetaUnitStatBonus
{
    public double baseMaxHp;
    public double baseAttackDamage;
    public float baseAttackSpeed;
    public double strength;
    public double agility;
    public double intelligence;
}

/// <summary>
/// Stores run-to-run meta upgrades purchased with Score.
/// </summary>
public class MetaProgressManager : MonoBehaviour
{
    private const string SaveKey = "MetaProgressState_v1";

    private static MetaProgressManager _instance;
    public static MetaProgressManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindObjectOfType<MetaProgressManager>();
            if (_instance == null)
            {
                var go = new GameObject("MetaProgressManager");
                _instance = go.AddComponent<MetaProgressManager>();
            }

            return _instance;
        }
    }

    [SerializeField] private MetaProgressState state = new MetaProgressState();

    [Header("Upgrade Costs")]
    [SerializeField] private UpgradeCostCurve startGoldCost = new UpgradeCostCurve(150, 1.15f, 999);
    [SerializeField] private UpgradeCostCurve baseMaxHpCost = new UpgradeCostCurve(100, 1.12f, 999);
    [SerializeField] private UpgradeCostCurve baseAttackDamageCost = new UpgradeCostCurve(120, 1.12f, 999);
    [SerializeField] private UpgradeCostCurve baseAttackSpeedCost = new UpgradeCostCurve(140, 1.12f, 999);
    [SerializeField] private UpgradeCostCurve strengthCost = new UpgradeCostCurve(300, 1.1f, 999);
    [SerializeField] private UpgradeCostCurve agilityCost = new UpgradeCostCurve(300, 1.1f, 999);
    [SerializeField] private UpgradeCostCurve intelligenceCost = new UpgradeCostCurve(300, 1.1f, 999);

    [Header("Bonus Per Level")]
    [SerializeField] private int startGoldPerLevel = 120;
    [SerializeField] private float baseMaxHpPerLevel = 40f;
    [SerializeField] private float baseAttackDamagePerLevel = 1.0f;
    [SerializeField] private float baseAttackSpeedPerLevel = 0.01f;
    [SerializeField] private double strengthPerLevel = 1.0;
    [SerializeField] private double agilityPerLevel = 1.0;
    [SerializeField] private double intelligencePerLevel = 1.0;

    public double Currency => state.unspentScore;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadState();
    }

    private void OnDestroy()
    {
        if (_instance == this) SaveState();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveState();
    }

    private void OnApplicationQuit()
    {
        SaveState();
    }

    public void AddScore(double amount)
    {
        if (amount <= 0) return;
        state.unspentScore += amount;
        SaveState();
    }

    public double GetNextCost(MetaUpgradeType type)
    {
        if (IsMaxLevel(type)) return double.PositiveInfinity;
        var curve = GetCurve(type);
        int level = GetLevel(type);
        return curve.GetCost(level);
    }

    public int GetLevel(MetaUpgradeType type)
    {
        return type switch
        {
            MetaUpgradeType.StartGold => state.startGoldLevel,
            MetaUpgradeType.BaseMaxHp => state.baseMaxHpLevel,
            MetaUpgradeType.BaseAttackDamage => state.baseAttackDamageLevel,
            MetaUpgradeType.BaseAttackSpeed => state.baseAttackSpeedLevel,
            MetaUpgradeType.Strength => state.strengthLevel,
            MetaUpgradeType.Agility => state.agilityLevel,
            MetaUpgradeType.Intelligence => state.intelligenceLevel,
            _ => 0
        };
    }

    public bool TryPurchase(MetaUpgradeType type, out double cost, out int newLevel)
    {
        cost = GetNextCost(type);
        newLevel = GetLevel(type);

        if (IsMaxLevel(type) || state.unspentScore < cost)
            return false;

        state.unspentScore -= cost;
        IncrementLevel(type);
        newLevel = GetLevel(type);
        SaveState();
        return true;
    }

    public int GetStartGoldBonus()
    {
        return Mathf.RoundToInt(startGoldPerLevel * state.startGoldLevel);
    }

    public MetaUnitStatBonus GetUnitBonus()
    {
        return new MetaUnitStatBonus
        {
            baseMaxHp = baseMaxHpPerLevel * state.baseMaxHpLevel,
            baseAttackDamage = baseAttackDamagePerLevel * state.baseAttackDamageLevel,
            baseAttackSpeed = baseAttackSpeedPerLevel * state.baseAttackSpeedLevel,
            strength = strengthPerLevel * state.strengthLevel,
            agility = agilityPerLevel * state.agilityLevel,
            intelligence = intelligencePerLevel * state.intelligenceLevel,
        };
    }

    public MetaUnitStatBonus GetUnitBonusPerLevel()
    {
        return new MetaUnitStatBonus
        {
            baseMaxHp = baseMaxHpPerLevel,
            baseAttackDamage = baseAttackDamagePerLevel,
            baseAttackSpeed = baseAttackSpeedPerLevel,
            strength = strengthPerLevel,
            agility = agilityPerLevel,
            intelligence = intelligencePerLevel,
        };
    }

    public int GetStartGoldPerLevel()
    {
        return startGoldPerLevel;
    }

    public bool IsMaxLevel(MetaUpgradeType type)
    {
        var curve = GetCurve(type);
        return curve.maxLevel > 0 && GetLevel(type) >= curve.maxLevel;
    }

    private void IncrementLevel(MetaUpgradeType type)
    {
        switch (type)
        {
            case MetaUpgradeType.StartGold: state.startGoldLevel++; break;
            case MetaUpgradeType.BaseMaxHp: state.baseMaxHpLevel++; break;
            case MetaUpgradeType.BaseAttackDamage: state.baseAttackDamageLevel++; break;
            case MetaUpgradeType.BaseAttackSpeed: state.baseAttackSpeedLevel++; break;
            case MetaUpgradeType.Strength: state.strengthLevel++; break;
            case MetaUpgradeType.Agility: state.agilityLevel++; break;
            case MetaUpgradeType.Intelligence: state.intelligenceLevel++; break;
        }
    }

    private UpgradeCostCurve GetCurve(MetaUpgradeType type)
    {
        return type switch
        {
            MetaUpgradeType.StartGold => startGoldCost,
            MetaUpgradeType.BaseMaxHp => baseMaxHpCost,
            MetaUpgradeType.BaseAttackDamage => baseAttackDamageCost,
            MetaUpgradeType.BaseAttackSpeed => baseAttackSpeedCost,
            MetaUpgradeType.Strength => strengthCost,
            MetaUpgradeType.Agility => agilityCost,
            MetaUpgradeType.Intelligence => intelligenceCost,
            _ => new UpgradeCostCurve(100, 1.1f, 999)
        };
    }

    private void SaveState()
    {
        var json = JsonUtility.ToJson(state);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            state = new MetaProgressState();
            return;
        }

        try
        {
            var json = PlayerPrefs.GetString(SaveKey);
            var loaded = JsonUtility.FromJson<MetaProgressState>(json);
            state = loaded ?? new MetaProgressState();
        }
        catch
        {
            state = new MetaProgressState();
        }
    }
}
