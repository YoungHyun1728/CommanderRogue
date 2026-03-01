using UnityEngine;
using System.Collections.Generic;

public enum VfxType
{
    TeleportSmoke,
    HealBurst,
    ShieldPop,
    BuffAura,
    DebuffHit,
    ProjectileHit,

    ThunderStrike,
    GoldBuff,
    HolyCross,
    Boom,
    None
}

[System.Serializable]
public class VfxPoolEntry
{
    public VfxType type;
    public VfxObject prefab;
    public int initialSize = 20;

    [HideInInspector] public Queue<VfxObject> pool = new Queue<VfxObject>();
}

public class VfxPoolManager : MonoBehaviour
{
    public static VfxPoolManager Instance { get; private set; }

    [SerializeField] private List<VfxPoolEntry> entries;
    private Dictionary<VfxType, VfxPoolEntry> entryDict;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        entryDict = new Dictionary<VfxType, VfxPoolEntry>();

        foreach (var e in entries)
        {
            entryDict[e.type] = e;

            for (int i = 0; i < e.initialSize; i++)
                CreateNew(e);
        }
    }

    private VfxObject CreateNew(VfxPoolEntry entry)
    {
        var vfx = Instantiate(entry.prefab, transform);
        vfx.gameObject.SetActive(false);
        vfx.SetPoolEntry(entry);
        entry.pool.Enqueue(vfx);
        return vfx;
    }

    public VfxObject Get(VfxType type, Vector3 position, Quaternion rotation)
    {
        if (!entryDict.TryGetValue(type, out var entry))
        {
            Debug.LogError($"[VFXPool] 등록되지 않은 타입: {type}");
            return null;
        }

        if (entry.pool.Count == 0)
            CreateNew(entry);

        var vfx = entry.pool.Dequeue();
        vfx.transform.SetPositionAndRotation(position, rotation);
        vfx.gameObject.SetActive(true);
        return vfx;
    }

    public void Release(VfxObject vfx, VfxPoolEntry entry)
    {
        vfx.gameObject.SetActive(false);
        vfx.transform.SetParent(transform);
        entry.pool.Enqueue(vfx);
    }
}