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
    [Tooltip("1보다 작으면 공격속도 배율. 예: 1.4 = 40% faster, 0.7 = 30% slower")]
    public float attackSpeedMultiplier = 1f;
}