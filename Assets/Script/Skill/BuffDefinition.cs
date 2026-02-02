using UnityEngine;

[CreateAssetMenu(menuName = "Game/Buff Definition")]
public class BuffDefinition : ScriptableObject
{
    public string buffId;
    public float duration = 5f;

    [Header("Stats (Flat)")]
    public double addStrength;
    public double addAgility;
    public double addIntelligence;

    [Header("Stats (Rate)")]
    public double addStrengthRate;
    public double addAgilityRate;
    public double addIntelligenceRate;

    [Header("Combat")]
    [Tooltip("1보다 작으면 공속 빨라짐(공격간격에 곱해짐). 예: 0.6 = 40% faster")]
    public float attackIntervalMultiplier = 1f;
}