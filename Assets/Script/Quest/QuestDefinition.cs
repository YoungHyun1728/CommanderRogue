using UnityEngine;

public enum QuestType
{
    EscortAlly, // 호위 퀘스트
    // 나중에 수집/토벌/생존 등 추가 가능
}

[CreateAssetMenu(menuName = "Game/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    public string questId;
    public string title;
    [TextArea] public string description;

    public QuestType type = QuestType.EscortAlly;

    [Header("Duration (Rounds)")]
    public int minRounds = 5;
    public int maxRounds = 20;

    [Header("Escort Ally")]
    public UnitData unitData; // 플레이어 유닛 프리팹(호위대상)
    public bool preventDuplicateAlly = true; // 이미 있으면 중복 스폰 방지
}