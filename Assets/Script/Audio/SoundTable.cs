using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BgmEntry
{
    public BgmId id;
    public AudioClip clip;
    public bool loop = true;
    [Range(0f, 2f)] public float volume = 1f;
}

[CreateAssetMenu(fileName = "BgmTable", menuName = "Audio/Bgm Table")]
public class BgmTable : ScriptableObject
{
    [SerializeField] private List<BgmEntry> entries = new List<BgmEntry>();
    private Dictionary<BgmId, BgmEntry> map;

    private void OnEnable()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        map = new Dictionary<BgmId, BgmEntry>();
        foreach (var entry in entries)
        {
            if (entry != null)
            {
                map[entry.id] = entry;
            }
        }
    }

    public BgmEntry Get(BgmId id)
    {
        if (map == null || map.Count == 0)
            BuildMap();

        map.TryGetValue(id, out var entry);
        return entry;
    }
}

[System.Serializable]
public class SfxEntry
{
    public SfxId id;
    public AudioClip clip;
    [Range(0f, 2f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitchMin = 0.95f;
    [Range(0.1f, 3f)] public float pitchMax = 1.05f;
    [Range(0f, 1f)] public float cooldown = 0.05f; // 동일 SFX 연타 방지

    [System.NonSerialized] public float lastPlayTime = -999f;
}

[CreateAssetMenu(fileName = "SfxTable", menuName = "Audio/Sfx Table")]
public class SfxTable : ScriptableObject
{
    [SerializeField] private List<SfxEntry> entries = new List<SfxEntry>();
    private Dictionary<SfxId, SfxEntry> map;

    private void OnEnable()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        map = new Dictionary<SfxId, SfxEntry>();
        foreach (var entry in entries)
        {
            if (entry != null)
            {
                map[entry.id] = entry;
            }
        }
    }

    public SfxEntry Get(SfxId id)
    {
        if (map == null || map.Count == 0)
            BuildMap();

        map.TryGetValue(id, out var entry);
        return entry;
    }
}

[System.Serializable]
public class VoiceEntry
{
    public VoiceId id;
    public AudioClip clip;
    [Range(0f, 2f)] public float volume = 1f;
}

[CreateAssetMenu(fileName = "VoiceTable", menuName = "Audio/Voice Table")]
public class VoiceTable : ScriptableObject
{
    [SerializeField] private List<VoiceEntry> entries = new List<VoiceEntry>();
    private Dictionary<VoiceId, VoiceEntry> map;

    private void OnEnable()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        map = new Dictionary<VoiceId, VoiceEntry>();
        foreach (var entry in entries)
        {
            if (entry != null)
            {
                map[entry.id] = entry;
            }
        }
    }

    public VoiceEntry Get(VoiceId id)
    {
        if (map == null || map.Count == 0)
            BuildMap();

        map.TryGetValue(id, out var entry);
        return entry;
    }
}
