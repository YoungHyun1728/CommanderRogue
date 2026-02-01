using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private DialogueDatabase database;
    [SerializeField] private DialoguePanel panel;
    private DialogueDefinition current;
    private DialogueNode currentNode;

    private bool waitingChoice;

    // 외부에서 넘길 수 있는 컨텍스트(선택 액션이 게임 상태를 조작할 때 사용)
    private DialogueContext ctx;

    private Action onComplete;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (panel) panel.Hide();
    }

    void Update()
    {
        if (current == null || waitingChoice) return;

        // 클릭/스페이스/엔터로 다음 진행 (선택지 없는 노드에서만)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            Advance();
        }
    }

    // ID로 다이얼로그 시작. 이 함수를 호출해야 시작 가능
    public void StartById(string id, DialogueContext ctx = null, System.Action onComplete = null)
    {
        if (database == null) { Debug.LogError("DialogueDatabase not set"); return; }
        var def = database.Get(id);
        if (def == null) { Debug.LogError($"Dialogue not found: {id}"); return; }
        StartDialogue(def, ctx, onComplete);
    }

    public void StartDialogue(DialogueDefinition def, DialogueContext context = null, Action onComplete = null)
    {
        current = def;
        ctx = context ?? new DialogueContext();
        this.onComplete = onComplete;

        if (current == null)
        {
            End();
            return;
        }

        currentNode = current.GetNode(current.entryNodeId);
        if (currentNode == null)
        {
            Debug.LogError($"[Dialogue] entryNodeId '{current.entryNodeId}' not found");
            End();
            return;
        }

        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (currentNode == null)
        {
            End();
            return;
        }

        waitingChoice = currentNode.choices != null && currentNode.choices.Count > 0;
        panel.ShowNode(currentNode, OnChoicePicked);
    }

    private void Advance()
    {
        // 선택지가 없으면 nextNodeId로 이동, 없으면 종료
        if (currentNode == null) { End(); return; }

        if (!string.IsNullOrEmpty(currentNode.nextNodeId))
        {
            var next = current.GetNode(currentNode.nextNodeId);
            if (next == null)
            {
                Debug.LogError($"[Dialogue] nextNodeId '{currentNode.nextNodeId}' not found");
                End();
                return;
            }
            currentNode = next;
            ShowCurrent();
        }
        else
        {
            End();
        }
    }

    private void OnChoicePicked(int choiceIndex)
    {
        if (currentNode == null) { End(); return; }
        if (choiceIndex < 0 || choiceIndex >= currentNode.choices.Count) return;

        var choice = currentNode.choices[choiceIndex];

        // 1) 액션 실행(골드 부족 같은 실패면 여기서 return해서 대화 유지 가능)
        if (!ExecuteActions(choice))
            return;

        // 2) 분기 이동
        waitingChoice = false;

        if (!string.IsNullOrEmpty(choice.nextNodeId))
        {
            var next = current.GetNode(choice.nextNodeId);
            if (next == null)
            {
                Debug.LogError($"[Dialogue] choice nextNodeId '{choice.nextNodeId}' not found");
                End();
                return;
            }
            currentNode = next;
            ShowCurrent();
        }
        else
        {
            End();
        }
    }

    
    private bool ExecuteActions(DialogueChoice choice)
    {
        if (choice.actions == null) return true;

        foreach (var act in choice.actions)
        {
            switch (act.type)
            {
                case DialogueActionType.None:
                    break;

                case DialogueActionType.SpendGold:
                {
                    int cost = act.intValue;
                    if (ctx.runManager == null) break;

                    if (ctx.runManager.gold < cost)
                    {
                        // 실패 처리: 대화 유지
                        ToastManager.Instance?.Show("골드가 부족합니다.");
                        return false;
                    }
                    ctx.runManager.gold -= cost;
                    break;
                }

                case DialogueActionType.HealPartyFull:
                {
                    ctx.runManager?.HealPartyFull();
                    break;
                }

                case DialogueActionType.ShowToast:
                {
                    ToastManager.Instance?.Show(act.stringValue);
                    break;
                }

                case DialogueActionType.StartBanditBattle:
                {
                    // 전투 시작은 보통 '대화 종료 후'가 깔끔하니,
                    // 여기서 바로 시작하지 말고 ctx에 예약해두는 방식 추천
                    ctx.pendingBanditPresetKey = act.intValue;
                    break;
                }

                case DialogueActionType.GoToNextRound:
                {
                    ctx.pendingGoNextRound = true;
                    break;
                }
            }
        }
        return true;
    }

    private void End()
    {
        panel.Hide();

        // 예약된 외부 행동 실행 (대화가 닫힌 뒤 실행)
        if (ctx != null)
        {
            if (ctx.pendingBanditPresetKey >= 0 && ctx.runManager != null)
                ctx.runManager.StartEventBanditBattle(ctx.pendingBanditPresetKey);

            if (ctx.pendingGoNextRound && ctx.runManager != null)
                ctx.runManager.GoToNextRound();
        }

        var cb = onComplete;

        current = null;
        currentNode = null;
        waitingChoice = false;
        ctx = null;
        onComplete = null;

        cb?.Invoke();
    }
}

    
[Serializable]
public class DialogueContext
{
    public RunManager runManager;

    // 대화 끝나고 실행할 예약 동작들
    public int pendingBanditPresetKey = -1;
    public bool pendingGoNextRound = false;
}
