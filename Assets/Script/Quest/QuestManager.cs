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
                int penalty = q.TotalRounds * 1000;
                RunManager.Instance.gold = Mathf.Max(0, RunManager.Instance.gold - penalty);

                q.OnFail();
                active.RemoveAt(i);                
                ToastManager.Instance?.Show($"고블린 호위 실패! \n치료비를 물어줬습니다. \n-{penalty}G");
                continue;
            }

            q.RemainingRounds--;

            if (q.RemainingRounds <= 0)
            {
                int reward = q.TotalRounds * 1000;
                RunManager.Instance.gold += reward;

                q.OnComplete();
                active.RemoveAt(i);

                ToastManager.Instance?.Show($"호위 임무 완료!\n보상: +{reward}G");
            }
        }
    }
}

public class QuestInstance
{
    public QuestDefinition Def { get; }
    public int RemainingRounds { get; set; }
    public int TotalRounds { get; }
    private GameObject escortGo; //호위 대상 오브젝트
    private Unit escortUnit;

    public QuestInstance(QuestDefinition def, int totalRounds)
    {
        Def = def;
        TotalRounds = totalRounds;
        RemainingRounds = totalRounds;
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
                    escortUnit = escortGo.GetComponent<Unit>();
                    return;
                }
            }
        }

        escortGo = RunManager.Instance.SpawnUnit(Def.unitData);
        escortUnit = escortGo != null ? escortGo.GetComponent<Unit>() : null;
    }

    public void OnComplete()
    {
        if (Def.type != QuestType.EscortAlly) return;
        DespawnEscort();
    }

    public void OnFail()
    {
        DespawnEscort(); // 캐릭터 처리
    }

    private void DespawnEscort()
    {
        RunManager.Instance.playerUnits.RemoveAll(go => go == null);

        if (escortGo == null) return;

        RunManager.Instance.playerUnits.Remove(escortGo);
        GameObject.Destroy(escortGo);
        escortGo = null;
        escortUnit = null;
    }

    public bool IsEscortAlive()
    {
        if (escortGo == null) return false;
        if (!escortGo.activeInHierarchy) return false;     // 비활성이면 실패
        if (escortUnit != null && escortUnit.hp <= 0) return false; // hp 0이면 실패
        return true;
    }
}
