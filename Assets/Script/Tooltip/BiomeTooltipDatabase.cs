using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Biome Tooltip Database")]
public class BiomeTooltipDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public BiomeType biome;
        [TextArea(2, 6)] public string description;
        [TextArea(2, 6)] public string effect; 
    }


    public List<Entry> entries = new();

    // 런타임 조회 편하게 캐시
    private Dictionary<BiomeType, Entry> _map;

    void OnEnable()
    {
        _map = new Dictionary<BiomeType, Entry>();
        foreach (var e in entries)
        {
            if (!_map.ContainsKey(e.biome))
                _map.Add(e.biome, e);
        }
    }

    public string GetDesc(BiomeType biome)
    {
        if (_map != null && _map.TryGetValue(biome, out var entry))
            return entry.description;

        foreach (var e in entries)
            if (e.biome.Equals(biome)) return e.description;

        return "설명이 준비되지 않았습니다.";
    }

    public string GetEffect(BiomeType biome)
    {
        if (_map != null && _map.TryGetValue(biome, out var entry))
            return entry.effect;

        foreach (var e in entries)
            if (e.biome.Equals(biome)) return e.effect;

        return "";
    }
}