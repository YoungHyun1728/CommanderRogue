using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 현재는 퀘스트가 고블린 상인 호위퀘스트 1개뿐이라서
/// 호위퀘스트에 맞춰서 구현
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private readonly List<QuestInstance> active = new();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public bool HasQuest(string questId) => active.Exists(q => q.Def != null && q.Def.questId == questId);

    public void StartQuest(QuestDefinition def)
    {
        if (def == null) return;

        // 같은 퀘스트가 이미 있으면 연장
        var existing = active.Find(q => q.Def != null && q.Def.questId == def.questId);
        if (existing != null)
        {
            // "또 도와달라" 같은 상황이면 연장
            int extra = Random.Range(def.minRounds, def.maxRounds + 1);
            existing.RemainingRounds += extra;
            ToastManager.Instance?.Show($"{def.title} 기간 연장 (+{extra} 라운드)");
            return;
        }

        int duration = Random.Range(def.minRounds, def.maxRounds + 1);
        var inst = new QuestInstance(def, duration);
        active.Add(inst);

        inst.OnStart();
        ToastManager.Instance?.Show($"퀘스트 시작: {def.title} ({duration} 라운드)");
    }

    // 라운드가 증가할때마다 호출해야하는 함수
    public void OnRoundAdvanced()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var q = active[i];

            // 실패 체크 먼저
            if (q.Def.type == QuestType.EscortAlly && !q.IsEscortAlive())
            {
                q.OnFail();
                active.RemoveAt(i);
                continue;
            }

            q.RemainingRounds--;

            if (q.RemainingRounds <= 0)
            {
                q.OnComplete();
                ToastManager.Instance?.Show($"호위 임무 완료!");
                active.RemoveAt(i);
            }
        }

    }

}

public class QuestInstance
{
    public QuestDefinition Def { get; }
    public int RemainingRounds { get; set; }
    private GameObject escortGo; //호위 대상 오브젝트

    public QuestInstance(QuestDefinition def, int remainingRounds)
    {
        Def = def;
        RemainingRounds = remainingRounds;
    }

    public void OnStart()
    {
        if (Def.type != QuestType.EscortAlly) return;

        if (Def.preventDuplicateAlly)
        {
            foreach (var go in RunManager.Instance.playerUnits)
            {
                if (go == null) continue;
                if (go.name.Contains(Def.unitData.unitName))
                {
                    escortGo = go; 
                    return;
                }
            }
        }

        escortGo = RunManager.Instance.SpawnUnit(Def.unitData);
    }

    public void OnComplete()
    {
        if (Def.type != QuestType.EscortAlly) return;
        DespawnEscort();
    }

    public void OnFail()
    {
        ToastManager.Instance?.Show("고블린 호위 실패!");
        DespawnEscort(); // 원하면 실패 시엔 안 지워도 되지만, 안전하게 처리
    }

    private void DespawnEscort()
    {
        if (escortGo == null) return;

        RunManager.Instance.playerUnits.Remove(escortGo);
        GameObject.Destroy(escortGo);
        escortGo = null;
    }

    public bool IsEscortAlive() => escortGo != null;
}
