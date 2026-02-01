using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Game/Dialogue/Dialogue Database")]
public class DialogueDatabase : ScriptableObject
{
    public List<DialogueDefinition> dialogues = new();

    public DialogueDefinition Get(string dialogueId)
    {
        return dialogues.Find(d => d != null && d.dialogueId == dialogueId);
    }
}