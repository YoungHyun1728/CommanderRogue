using UnityEngine;
using System.Collections.Generic;

public enum ProjectileType // 기본 공격 투사체 (추가시 프리팹만들어서 추가)
{
    //기본공격 투사체
    Shuriken,
    Arrow,
    Energyball,
    Stone,
    Coin,           

    //스킬 투사체
    FireballSkill,
    AcidBlobSkill,
    LightningOrbSkill,
    PoisonWaveSkill,
    FireFlameSkill,
    BigShurikenSkill,
    TornadoSkill
}

[System.Serializable]
public class ProjectilePoolEntry
{
    public ProjectileType type;     // 표창/화살/파이어볼 등
    public Projectile prefab;       // 해당 타입의 프리팹
    public int initialSize = 20;

    [HideInInspector] public Queue<Projectile> pool = new Queue<Projectile>();
}

public class ProjectilePoolManager : MonoBehaviour
{
    public static ProjectilePoolManager Instance { get; private set; }

    [SerializeField] private List<ProjectilePoolEntry> entries;
    private Dictionary<ProjectileType, ProjectilePoolEntry> entryDict;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        entryDict = new Dictionary<ProjectileType, ProjectilePoolEntry>();
        foreach (var e in entries)
        {
            entryDict[e.type] = e;

            // 미리 생성
            for (int i = 0; i < e.initialSize; i++)
            {
                CreateNew(e);
            }
        }
    }

    private Projectile CreateNew(ProjectilePoolEntry entry)
    {
        var proj = Instantiate(entry.prefab, transform);
        proj.gameObject.SetActive(false);
        proj.SetPoolEntry(entry);   
        entry.pool.Enqueue(proj);

        return proj;
    }

    public Projectile Get(ProjectileType type, UnitFSM shooter, Quaternion rotation)
    {
        Vector3 pos = Vector3.zero;

        if (shooter != null)
        {
            pos = shooter.GetProjectileSpawnWorldPos();
        }

        return Get(type, pos, rotation);
    }

    public Projectile Get(ProjectileType type, Vector3 position, Quaternion rotation)
    {
        if (!entryDict.TryGetValue(type, out var entry))
        {
            Debug.LogError($"[ProjectilePool] 등록되지 않은 타입: {type}");
            return null;
        }

        if (entry.pool.Count == 0)
        {
            CreateNew(entry);
        }

        var proj = entry.pool.Dequeue();
        proj.transform.SetPositionAndRotation(position, rotation);
        proj.gameObject.SetActive(true);
        return proj;
    }

    public void Release(Projectile projectile, ProjectilePoolEntry entry)
    {
        projectile.gameObject.SetActive(false);
        entry.pool.Enqueue(projectile);
    }
}
