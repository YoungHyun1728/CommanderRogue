using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Game/Dialogue/Unit Spawn Speech DB")]
public class UnitSpawnSpeechDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string unitName;                // 키: UnitData.unitName
        [TextArea] public List<string> lines;  // 해당 유닛이 말할 수 있는 대사들
    }

    public List<Entry> entries = new();

    public string GetLine(string unitName)
    {
        if (string.IsNullOrEmpty(unitName)) return null;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.unitName != unitName) continue;
            if (e.lines == null || e.lines.Count == 0) return null;

            // 여러 줄이면 랜덤 1줄
            return e.lines[UnityEngine.Random.Range(0, e.lines.Count)];
        }
        return null;
    }
}
