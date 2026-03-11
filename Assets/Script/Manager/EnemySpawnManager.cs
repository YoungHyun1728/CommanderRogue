using UnityEngine;
using System.Collections.Generic;

public class EnemySpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class BiomeEnemyList
    {
        public BiomeType biome;

        [Header("일반 적 리스트")]
        public List<UnitData> normalEnemies;  // 바이옴 잡몹들

        [Header("보스 리스트 (기본폼/각성폼 포함)")]
        public List<UnitData> bossEnemies;    // 이 바이옴의 보스 유닛들
    }

    [System.Serializable]
    public class FixedBattlePreset
    {
        public int keyRoundOrNode;          // 8,25,... 또는 노드 인덱스
        public List<UnitData> enemies;      // 고정 스폰 리스트
    }

    [System.Serializable]
    public class EquipmentTierPool
    {
        [Header("장비풀 이름")]
        public string tierName;

        [Tooltip("가중치")]
        public float weight = 1f;

        [Tooltip("이 등급에 속한 장비들")]
        public List<Equipment> equipments = new();
    }

    [Header("네크로맨서 프리셋")]
    public List<FixedBattlePreset> necromancerPresets;
    [Header("바이옴 에너미 프리셋")]
    public List<BiomeEnemyList> biomeEnemyLists;
    [Header("이벤트 전투(도적단) 프리셋 - key는 노드 인덱스나 바이옴 인덱스")]
    public List<FixedBattlePreset> banditPresets;

    [Header("===== 적 전용 장비 드랍(자동 착용) =====")]
    [Tooltip("켜면 적이 라운드에 따라 장비를 끼고 등장")]
    [SerializeField] private bool enableEnemyEquipment = true;
    [Tooltip("이 라운드부터 장비 지급 시작")]
    [SerializeField] private int equipmentStartRound = 10;
    [Tooltip("몇 라운드마다 장비 1개씩 추가할지")]
    [SerializeField] private int equipmentIntervalRound = 10;
    [Tooltip("한 적이 최대 몇 개까지 착용할지")]
    [SerializeField] private int maxEquipmentPerEnemy = 10;
    [Header("장비 롤 성공 확률(꽝 포함)")]
    [Tooltip("1이면 항상 지급, 0.5면 각 롤마다 50% 확률로 스킵(꽝)")]
    [Range(0f, 1f)] [SerializeField] private float equipmentRollSuccessRate = 0.7f;
    [Tooltip("true면 같은 장비 중복 허용")]
    [SerializeField] private bool allowDuplicateEquipment = true;
    [Tooltip("등급/가중치별 장비 풀")]
    public List<EquipmentTierPool> enemyEquipmentPools = new();

    [SerializeField] private TileMapManager tileMapManager;
    [SerializeField] private RunManager runManager;

    public enum BossLine
    {
        None = 0,
        A = 1,
        B = 2
    }

    public BossLine LastBossLine { get; private set; } = BossLine.None;
    public int LastBossIndex { get; private set; } = 0;
    public int LastNecromancerIndex { get; private set; } = 1;
    public enum BossCategory { Necromancer, BiomeBoss }
    public BossCategory LastBossCategory => LastBossIndex == 
        0 ? BossCategory.Necromancer : BossCategory.BiomeBoss;

    BiomeEnemyList GetBiomeList(BiomeType biome)
    {
        return biomeEnemyLists.Find(b => b.biome == biome);
    }
    public List<GameObject> SpawnBattle(BiomeType biome, int roundNumber, bool isBossRound)
    {
        return SpawnBattle(biome, roundNumber, isBossRound, 0);
    }

    public List<GameObject> SpawnBattle(BiomeType biome, int roundNumber, bool isBossRound, int enemyLevelOffset)
    {
        if (isBossRound)
        {
            if (IsNecromancerRound(RunManager.Instance.currentLevel))
                return SpawnNecromancerBattle(roundNumber, enemyLevelOffset);

            var result = new List<GameObject>();
            result.AddRange(SpawnBossBattle(biome, roundNumber, enemyLevelOffset));
            // 보스스폰 후 일반몹도 스폰
            result.AddRange(SpawnNormalBattle(biome, roundNumber, enemyLevelOffset));

            return result;
        }

        return SpawnNormalBattle(biome, roundNumber, enemyLevelOffset);
    }

    
    public List<GameObject> SpawnNormalBattle(BiomeType biome, int roundLevel)
    {
        return SpawnNormalBattle(biome, roundLevel, 0);
    }


    public List<GameObject> SpawnNormalBattle(BiomeType biome, int roundLevel, int enemyLevelOffset)
    {
        var result = new List<GameObject>();
        var biomeList = GetBiomeList(biome);

        if (biomeList == null || biomeList.normalEnemies.Count == 0)
            return result;

        int count = Mathf.Min(12, Random.Range(1, 4) + (roundLevel / 20));

        for (int i = 0; i < count; i++)
        {
            UnitData enemyData =
                biomeList.normalEnemies[Random.Range(0, biomeList.normalEnemies.Count)];

            bool isMelee = enemyData.attackRange <= 1;

            Vector2Int tile;
            if (!tileMapManager.TryGetEnemySpawnTile(isMelee, out tile))
            {
                Debug.LogWarning("[EnemySpawnManager] 스폰 실패, 유닛 스킵");
                continue;
            }

            Vector3 world = tileMapManager.tilemap.GetCellCenterWorld(new Vector3Int(tile.x, tile.y, 0));
            GameObject go = Instantiate(enemyData.prefab, world, Quaternion.identity);

            UnitFSM fsm = go.GetComponent<UnitFSM>();
            fsm.Initialize(tileMapManager, tile);

            Unit unit = go.GetComponent<Unit>();
            unit.ApplyData(enemyData);

            // 라운드(전투 종류/보스 선택)는 roundLevel로 유지하고,
            // 실제 강해지는 정도만 enemyLevelOffset으로 반영
            int targetLevel = Mathf.Max(1, Mathf.RoundToInt(roundLevel * Random.Range(0.60f, 0.86f)) + enemyLevelOffset);
            int levelGain = Mathf.Max(0, targetLevel - enemyData.level);
            unit.SetLevel(levelGain);
            TryEquipEnemy(unit, roundLevel);

            result.Add(go);
            tileMapManager.enemyUnits.Add(go);
            runManager.enemyUnits.Add(go);
        }

        return result;
    }


    
    public List<GameObject> SpawnBossBattle(BiomeType biome, int roundLevel)
    {
        return SpawnBossBattle(biome, roundLevel, 0);
    }


    public List<GameObject> SpawnBossBattle(BiomeType biome, int roundLevel, int enemyLevelOffset)
    {
        var result = new List<GameObject>();
        var biomeList = GetBiomeList(biome);

        if (biomeList == null || biomeList.bossEnemies.Count == 0)
            return result;

        int bossIndex = Mathf.Max(1, roundLevel / 20); // 몇번째 보스전인지
        LastBossIndex = bossIndex;
        var bossesToSpawn = SelectBossSet(biomeList.bossEnemies, bossIndex);

        foreach (var bossData in bossesToSpawn)
        {
            bool isMelee = bossData.attackRange <= 1;

            Vector2Int tile;
            if (!tileMapManager.TryGetEnemySpawnTile(isMelee, out tile))
            {
                Debug.LogWarning("[EnemySpawnManager] 보스 스폰 실패, 유닛 스킵");
                continue;
            }

            Vector3 world = tileMapManager.tilemap.GetCellCenterWorld(new Vector3Int(tile.x, tile.y, 0));
            GameObject go = Instantiate(bossData.prefab, world, Quaternion.identity);

            var fsm = go.GetComponent<UnitFSM>();
            fsm.Initialize(tileMapManager, tile);

            var unit = go.GetComponent<Unit>();
            unit.ApplyData(bossData);

            int targetLevel = Mathf.Max(1, Mathf.RoundToInt(roundLevel * Random.Range(0.90f, 0.96f)) + enemyLevelOffset);
            int levelGain = Mathf.Max(0, targetLevel - bossData.level);
            unit.SetLevel(levelGain);
            TryEquipEnemy(unit, roundLevel);

            result.Add(go);
            tileMapManager.enemyUnits.Add(go);
            runManager.enemyUnits.Add(go);
        }

        return result;
    }


    // 보스 라운드 등장할 유닛 세트 선택
    private List<UnitData> SelectBossSet(List<UnitData> list, int bossIndex)
    {
        UnitData A  = list.Count > 0 ? list[0] : null;
        UnitData B  = list.Count > 1 ? list[1] : null;
        UnitData Aw = list.Count > 2 ? list[2] : null; // A 각성
        UnitData Bw = list.Count > 3 ? list[3] : null; // B 각성

        var res = new List<UnitData>();
        LastBossLine = BossLine.None;

        // 1~3: A/B 중 1마리
        if (bossIndex <= 3)
        {
            UnitData picked = (Random.value < 0.5f) ? (A ?? B) : (B ?? A);
            if (picked != null) res.Add(picked);

            LastBossLine = (picked == A) ? BossLine.A : BossLine.B;

            return res;
        }

        // 4~5: A/B각성 중 하나
        if (bossIndex <= 5)
        {
            var pool = new List<UnitData>();
            if (Aw != null) pool.Add(Aw);
            if (Bw != null) pool.Add(Bw);

            if (pool.Count == 0)
            {
                if (A != null) { res.Add(A); LastBossLine = BossLine.A; }
                else if (B != null) { res.Add(B); LastBossLine = BossLine.B; }
                return res;
            }

            UnitData picked = pool[Random.Range(0, pool.Count)];
            res.Add(picked);

            LastBossLine = (picked == Aw) ? BossLine.A : BossLine.B;
            return res;
        }

        // 6~7: A각성 + B
        if (bossIndex <= 7)
        {
            bool pickAline = Random.value < 0.5f;

            if (pickAline)
            {
                // Aw + B
                if (Aw != null) res.Add(Aw);
                if (B != null)  res.Add(B);
                LastBossLine = BossLine.A;   // "A계열 보스전"으로 취급
            }
            else
            {
                // Bw + A
                if (Bw != null) res.Add(Bw);
                if (A != null)  res.Add(A);
                LastBossLine = BossLine.B;   // "B계열 보스전"으로 취급
            }

            // 혹시나 둘 다 null이면 보험
            if (res.Count == 0 && A != null) { res.Add(A); LastBossLine = BossLine.A; }
            if (res.Count == 0 && B != null) { res.Add(B); LastBossLine = BossLine.B; }

            return res;
        }

        // 8~9: Aw + Bw (둘 다)
        if (Aw != null) res.Add(Aw);
        if (Bw != null) res.Add(Bw);

        if (res.Count == 0)
        {
            if (A != null) res.Add(A);
            else if (B != null) res.Add(B);
        }

        LastBossLine = BossLine.A; // "A계열 보스전"으로 취급
        return res;
    }

    // 네크로맨서 배틀 관련 함수
    public bool IsNecromancerRound(int roundNumber)
    {
        return roundNumber == 8 || roundNumber == 25 || roundNumber == 55 ||
            roundNumber == 95 || roundNumber == 145 || roundNumber == 200;
    }

    public List<GameObject> SpawnNecromancerBattle(int roundNumber, int enemyLevelOffset = 0)
    {
        LastBossLine = BossLine.None;
        LastBossIndex = 0; // 네크로맨서 라운드는 일반 보스 인덱스를 0으로 리셋해 구분
        // 네크로맨서 조우시 인덱스 설정
        LastNecromancerIndex = roundNumber == 8 ? 1 :
                               roundNumber == 25 ? 2 :
                               roundNumber == 55 ? 3 :
                               roundNumber == 95 ? 4 :
                               roundNumber == 145 ? 5 :
                               roundNumber == 200 ? 6 : 1;

        var result = new List<GameObject>();
        var preset = necromancerPresets.Find(p => p.keyRoundOrNode == roundNumber);
        if (preset == null || preset.enemies == null || preset.enemies.Count == 0) 
            return result;

        for (int i = 0; i < preset.enemies.Count; i++)
            SpawnOneFixed(preset.enemies[i], roundNumber, enemyLevelOffset, result, isFirstEnemy: i == 0);

        return result;
    }

    // 고정 전투용 스폰 함수 (풀에 있는 유닛 전부 소환)
    private void SpawnOneFixed(UnitData data, int roundNumber, int enemyLevelOffset, List<GameObject> result, bool isFirstEnemy)
    {
        bool isMelee = data.attackRange <= 1;

        Vector2Int tile;
        if (!tileMapManager.TryGetEnemySpawnTile(isMelee, out tile))
        {
            Debug.LogWarning("[EnemySpawnManager] 고정전투 스폰 실패");
            return;
        }

        Vector3 world = tileMapManager.tilemap.GetCellCenterWorld(new Vector3Int(tile.x, tile.y, 0));
        GameObject go = Instantiate(data.prefab, world, Quaternion.identity);

        var fsm = go.GetComponent<UnitFSM>();
        fsm.Initialize(tileMapManager, tile);

        var unit = go.GetComponent<Unit>();
        unit.ApplyData(data);

        int baseLevel = isFirstEnemy
            ? Mathf.Max(1, roundNumber)
            : Mathf.Max(1, Mathf.RoundToInt(roundNumber * Random.Range(0.70f, 0.86f)));
        int targetLevel = Mathf.Max(1, baseLevel + enemyLevelOffset);
        int levelGain = Mathf.Max(0, targetLevel - data.level);
        unit.SetLevel(levelGain);
        TryEquipEnemy(unit, roundNumber);

        result.Add(go);
        tileMapManager.enemyUnits.Add(go);
        runManager.enemyUnits.Add(go);
    }

    // 이벤트 - 도적단 전투 
    public List<GameObject> SpawnBanditBattle(int nodeIndexOrTier, int enemyLevelOffset = 0)
    {
        var result = new List<GameObject>();
        var preset = banditPresets.Find(p => p.keyRoundOrNode == nodeIndexOrTier);
        if (preset == null || preset.enemies == null || preset.enemies.Count == 0) return result;

        for (int i = 0; i < preset.enemies.Count; i++)
            SpawnOneFixed(preset.enemies[i], nodeIndexOrTier, enemyLevelOffset, result, isFirstEnemy: i == 0);

        return result;
    }

    // ----------- 적 장비 지급 관련 -----------
    void TryEquipEnemy(Unit unit, int roundLevel)
    {
        if (!enableEnemyEquipment || unit == null) return;

        var equipments = RollEnemyEquipments(roundLevel);
        if (equipments.Count == 0) return;

        foreach (var eq in equipments)
        {
            if (eq == null) continue;
            unit.Equip(eq);
        }
    }

    List<Equipment> RollEnemyEquipments(int roundLevel)
    {
        var result = new List<Equipment>();

        if (enemyEquipmentPools == null || enemyEquipmentPools.Count == 0) return result;
        if (roundLevel < equipmentStartRound) return result;
        if (maxEquipmentPerEnemy <= 0) return result;

        int interval = Mathf.Max(1, equipmentIntervalRound);
        int count = 1 + Mathf.FloorToInt((roundLevel - equipmentStartRound) / (float)interval);
        count = Mathf.Clamp(count, 1, maxEquipmentPerEnemy);

        int guard = 0;
        for (int i = 0; i < count && result.Count < maxEquipmentPerEnemy; i++)
        {
            guard++;
            if (guard > 50) break; // 무한루프 방지

            // 개별 롤 성공 확률
            if (Random.value > equipmentRollSuccessRate) continue;

            Equipment pick = PickOneEquipmentFromPools();
            if (pick == null) break;

            if (!allowDuplicateEquipment && result.Contains(pick))
                continue;

            result.Add(pick);
        }

        return result;
    }

    Equipment PickOneEquipmentFromPools()
    {
        // 비어있는 풀 제외
        var valid = enemyEquipmentPools.FindAll(p => p != null && p.equipments != null && p.equipments.Count > 0 && p.weight > 0f);
        if (valid.Count == 0) return null;

        float total = 0f;
        foreach (var p in valid) total += Mathf.Max(0.0001f, p.weight);

        float roll = Random.Range(0f, total);
        float acc = 0f;

        foreach (var p in valid)
        {
            acc += Mathf.Max(0.0001f, p.weight);
            if (roll <= acc)
            {
                int idx = Random.Range(0, p.equipments.Count);
                return p.equipments[idx];
            }
        }

        // fallback
        var last = valid[valid.Count - 1];
        return last.equipments[Random.Range(0, last.equipments.Count)];
    }
}
