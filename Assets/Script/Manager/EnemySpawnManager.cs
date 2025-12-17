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

    public List<BiomeEnemyList> biomeEnemyLists;

    [SerializeField] private TileMapManager tileMapManager;
    [SerializeField] private RunManager runManager;

    BiomeEnemyList GetBiomeList(BiomeType biome)
    {
        return biomeEnemyLists.Find(b => b.biome == biome);
    }

    public List<GameObject> SpawnNormalBattle(BiomeType biome, int roundLevel)
    {
        var result = new List<GameObject>();
        var biomeList = GetBiomeList(biome);

        if (biomeList == null || biomeList.normalEnemies.Count == 0)
            return result;

        // 일단 테스트: 1~3마리 랜덤
        int count = Random.Range(1, 4);

        for (int i = 0; i < count; i++)
        {
            UnitData enemyData =
                biomeList.normalEnemies[Random.Range(0, biomeList.normalEnemies.Count)];


            bool isMelee = (enemyData.attackRange > 1) ? false : true;
            GameObject go = Instantiate(enemyData.prefab);

            Vector2Int tile;
            if (!tileMapManager.TryGetEnemySpawnTile(isMelee, out tile))
            {
                Debug.LogWarning("[EnemySpawnManager] 스폰 실패, 유닛 스킵");
                Destroy(go);
                continue;
            }
            UnitFSM fsm = go.GetComponent<UnitFSM>();
            fsm.Initialize(tileMapManager, tile);

            Unit unit = go.GetComponent<Unit>();
            int enemyLevel = roundLevel; // (스케일링 값 생각중)
            unit.ApplyData(enemyData);

            result.Add(go);
            tileMapManager.enemyUnits.Add(go);
            runManager.enemyUnits.Add(go);
        }

        return result;
    }

    public List<GameObject> SpawnBossBattle(BiomeType biome, int roundLevel)
    {
        var result = new List<GameObject>();
        var biomeList = GetBiomeList(biome);

        if (biomeList == null || biomeList.bossEnemies.Count == 0)
            return result;

        // 나중에 "몇 번째 보스전이냐" 보고 둘 다, 각성폼 등은 여기서 분기만 추가
        UnitData bossData =
            biomeList.bossEnemies[Random.Range(0, biomeList.bossEnemies.Count)];

        bool isMelee = (bossData.attackRange > 1) ? false : true;
        GameObject go = Instantiate(bossData.prefab);

        Vector2Int tile;
        tileMapManager.TryGetEnemySpawnTile(isMelee, out tile);
        UnitFSM fsm = go.GetComponent<UnitFSM>();
        fsm.Initialize(tileMapManager, tile);

        Unit unit = go.GetComponent<Unit>();
        int bossLevel = roundLevel + 3; // (스케일링 값 생각중)
        unit.ApplyData(bossData);
        unit.GainLevel(bossLevel - bossData.level);

        result.Add(go);
        tileMapManager.enemyUnits.Add(go);
        runManager.enemyUnits.Add(go);

        return result;
    }

}
