using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Dialogue/Dialogue Definition")]
public class DialogueDefinition : ScriptableObject
{
    public string dialogueId;

    [Tooltip("시작 노드 ID")]
    public string entryNodeId;

    public List<DialogueNode> nodes = new();

    public DialogueNode GetNode(string id)
    {
        return nodes.Find(n => n != null && n.nodeId == id);
    }
}

[Serializable]
public class DialogueNode
{
    public string nodeId;

    public string speaker;    
    [TextArea] public string text;
    public Sprite portrait;

    [Tooltip("선택지가 있으면 클릭/스페이스로 자동 다음 진행이 아니라 선택 대기")]
    public List<DialogueChoice> choices = new();

    [Tooltip("선택지가 없을 때 자동으로 다음으로 넘어갈 노드. 비어있으면 종료")]
    public string nextNodeId;
}

[Serializable]
public class DialogueChoice
{
    public string buttonText;

    [Tooltip("선택 시 이동할 노드. 비어있으면 종료")]
    public string nextNodeId;

    [Tooltip("선택 시 실행할 액션들")]
    public List<DialogueAction> actions = new();
}

public enum DialogueActionType
{
    None,
    SpendGold,
    HealPartyFull,
    StartBanditBattle,
    ShowToast,
    GoToNextRound,
}

[Serializable]
public class DialogueAction
{
    public DialogueActionType type;

    public int intValue;
    public string stringValue;
}