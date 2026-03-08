using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-1)] // 타일맵매니저가 AStarPathfinder보다 먼저 실행
public class TileMapManager : MonoBehaviour
{
    public Tilemap tilemap; // 타일맵
    private Dictionary<Vector2Int, List<UnitFSM>> moveIntents = new();
    private Dictionary<Vector2Int, int> reservedTiles = new Dictionary<Vector2Int, int>();
    public Tile highlightTile; // 배치 가능한 타일을 표시할 하이라이트 타일
    public List<GameObject> playerUnits = new List<GameObject>(); // 플레이어 유닛 리스트
    public List<GameObject> enemyUnits = new List<GameObject>(); // 적 유닛 리스트
    public List<TileData> tileDataList; // 타일의 포지션과 상태(배치가능유무, 장애물)를 관리

    [SerializeField, Tooltip("실시간 타일 상태 확인용")] 
    private List<string> tileStatusDisplay = new List<string>(); // 디버깅용 상태 표시 리스트

    // ===== Occupancy (유닛 점유) =====
    private readonly Dictionary<Vector2Int, int> tileToUnitId = new();
    private readonly Dictionary<int, Vector2Int> unitIdToTile = new();

    public Vector2Int tilemapOrigin; // 타일맵의 (0,0)

    
    void Start()
    {
        InitializeTileStatus();        
    }

    void Update()
    {
        UpdateTileStatusDisplay(); // 인스펙터에 표시용 데이터 갱신
    }

    //타일 상태 초기화
    void InitializeTileStatus()
    {
        BoundsInt bounds = tilemap.cellBounds; // 타일맵의 경계 초기화
        tilemapOrigin = new Vector2Int(bounds.xMin, bounds.yMin); // 왼쪽아래를 원점으로
        tileDataList = new List<TileData>();

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int tilePosition = new Vector3Int(x, y, 0);
                Vector2Int gridPosition = new Vector2Int(x, y); //2D 로 변환
                
                int initialStatus = tilemap.HasTile(tilePosition) ? 0 : -1; // 타일이 없으면 -1로 설정
                tileDataList.Add(new TileData(gridPosition, initialStatus));

                 // 타일이 있는 경우 BoxCollider2D 생성
                if (tilemap.HasTile(tilePosition))
                {
                    // 타일마다 GameObject 생성
                    GameObject tileObject = new GameObject($"Tile_{gridPosition}");
                    tileObject.transform.position = tilemap.GetCellCenterWorld(tilePosition); // 타일 중심 위치
                    tileObject.transform.parent = transform; // TileMapManager의 자식으로 설정

                    // BoxCollider2D 추가
                    BoxCollider2D collider = tileObject.AddComponent<BoxCollider2D>();
                    collider.isTrigger = true; // Trigger로 설정
                    collider.size = tilemap.cellSize; // 타일 크기와 일치

                    // 타일 데이터 저장
                    TileCollider tileData = tileObject.AddComponent<TileCollider>();
                    tileData.Position = gridPosition; // 좌표 정보 저장
                }
            }
        }

        Debug.Log("TileData 초기화 완료:");
        foreach (var tileData in tileDataList)
        {
            Debug.Log($"타일 {tileData.Position}: 상태 {tileData.Status}");
        }
    }

     // 모든 유닛의 위치를 기반으로 타일 상태를 업데이트
    public void UpdateTileStatus(Vector2Int currentTile)
    {
        // 타일맵의 모든 타일을 순회하며 상태 초기화
        for (int i = 0; i < tileDataList.Count; i++)
        {
            var pos = tileDataList[i].Position;
            bool has = tilemap.HasTile(new Vector3Int(pos.x, pos.y, 0));
            tileDataList[i] = new TileData(pos, has ? 0 : -1);
        }
        //유닛 위치 기반 상태 업데이트
        foreach (var unit in playerUnits)
        {
            if (unit == null) continue;
            var unitFsm = unit.GetComponent<UnitFSM>();
            if (unitFsm != null) SetTileStatus(unitFsm.currentTilePosition, -1);
        }
        
        foreach (var unit in enemyUnits)
        {
            if (unit == null) continue;
            var unitFsm = unit.GetComponent<UnitFSM>();
            if (unitFsm != null) SetTileStatus(unitFsm.currentTilePosition, -1);
        }
    }

    // 특정 타일의 상태를 설정
    public void SetTileStatus(Vector2Int position, int status)
    {
        bool tileFound = false; // 타일 변경 확인용

        for(int i = 0; i < tileDataList.Count; i++)
        {
            if (tileDataList[i].Position == position)
            {
                tileDataList[i] = new TileData(position, status); // 상태 업데이트
                //Debug.Log($"[TileMapManager] 타일 상태 변경: {position} -> {status}");
                tileFound = true; // 타일을 찾았음을 표시
                break;
            }
        }

        if(!tileFound)
        {
            Debug.LogWarning($"[TileMapManager] 타일 {position}을 찾을 수 없습니다!");
        }
    }

    // 특정 좌표에 대한 상태 가져오기
    public int GetTileStatus(Vector2Int position)
    {
        foreach (var tileData in tileDataList)
        {
            if (tileData.Position == position)
            {
                // (C) 여기! 기존 return tileData.Status; 바로 위에 끼워 넣는 자리
                if (tileData.Status == 0 && tileToUnitId.ContainsKey(position))
                    return -1;

                return tileData.Status;
            }
        }

        return -1; // tileDataList에 없는 타일 = 이동 불가
    }

    // 이동가능한 타일인지 확인
    public bool IsWalkable(Vector2Int tilePosition)
    {
        int status = GetTileStatus(tilePosition);
        if(status == 0)
        {
            return true;
        }

        return false;
    }

    // 선택가능한 타일은 하이라이트타일로 교체
    public void HighlightPlaceTiles()
    {
        BoundsInt bounds = tilemap.cellBounds;
        //Player가 배치할수있는 타일은 왼쪽 절반뿐이다.
        for(int x = 0; x < (bounds.xMax) / 2; x++)
        {
            for(int y = 0; y < bounds.yMax; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                int status = GetTileStatus(position);

                if(status == 0)
                {
                    Vector3Int setTilePosition = new Vector3Int(x, y, 0); // 절대 좌표로 변환
                    tilemap.SetTile(setTilePosition, highlightTile);
                }     
            }
        }
    }

    // 디버깅용 상태 표시 리스트 업데이트
    private void UpdateTileStatusDisplay()
    {
        tileStatusDisplay.Clear();
        foreach (var tileData in tileDataList)
        {
            tileStatusDisplay.Add($"Position: {tileData.Position}, Status: {tileData.Status}");
        }
    }

    // 월드 좌표를 타일 좌표로 변환
    public Vector2Int GetTileFromWorldPosition(Vector3 worldPosition)
    {
        Vector3Int cellPosition = tilemap.WorldToCell(worldPosition);
        return new Vector2Int(cellPosition.x, cellPosition.y);
    }
    /*
    public bool TryReserveTileForMove(Vector2Int tile, int unitId)
    {
        // 타일에 다른 유닛이 있으면 예약 불가
        if (GetTileStatus(tile) == -1)
        {
            return false;
        }

        // 아직 아무도 예약 안 했으면 예약 성공
        if (!reservedTiles.TryGetValue(tile, out int ownerId))
        {
            reservedTiles[tile] = unitId;
            return true;
        }

        // 이미 내가 예약한 타일이면 그냥 통과
        if (ownerId == unitId)
        {
            return true;
        }

        // 다른 유닛이 이미 선점한 타일이면 실패
        return false;
    }
    */

    // 이동이 끝났거나 취소될 때 예약 해제
    public void ReleaseReservedTile(Vector2Int tile, int unitId)
    {
        if (reservedTiles.TryGetValue(tile, out int ownerId) && ownerId == unitId)
        {
            reservedTiles.Remove(tile);
        }
    }

    // RunManager 플레이어에게 유닛 제공할 위치
    // 중앙부터 나선형으로 탐색하며 빈타일을 찾아줌
    public bool GetEmptyTile(out Vector2Int spawnTile)
    {
        int xMax = -1;
        int yMax = 2;
        int xMin = -6;
        int yMin = -3;

        //시작점
        spawnTile = new Vector2Int(-4, 0);

        if(GetTileStatus(spawnTile) == 0)
        {
            return true;
        }
        // 탐색을 위한 방향들
        Vector2Int[] dirs = new Vector2Int[]
        {
            new Vector2Int(1, 0),   // 오른쪽
            new Vector2Int(0, -1),  // 아래
            new Vector2Int(-1, 0),  // 왼쪽
            new Vector2Int(0, 1)    // 위
        };

        Vector2Int current = spawnTile;

        int stepLength = 1;          // 처음엔 1칸씩 이동
        int dirIndex = 0;            // 현재 방향 인덱스 (0 = 오른쪽)
        int dirChangeCount = 0;      // 방향 몇 번 바꿨는지
        int visited = 1;             // 검사한 타일 개수 (start 포함)
        int maxTiles = (xMax - xMin + 1) * (yMax - yMin + 1); // 36

        while(visited < maxTiles)
        {
            for(int step = 0; step < stepLength; step++)
            {
                current += dirs[dirIndex];

                if(current.x >= xMin && current.x <= xMax &&
                    current.y >= yMin && current.y <= yMax)
                {
                    visited++;

                    if(GetTileStatus(current) == 0)
                    {
                        spawnTile = current;
                        return true;
                    }

                    if(visited >= maxTiles)
                        break;
                }
            }

            //방향변경
            dirIndex = (dirIndex + 1) % 4;
            dirChangeCount++;

            //방향이 두번 바뀌면 이동칸수 증가
            if(dirChangeCount % 2 == 0)
            {
                stepLength++;
            }
        }

        //루프가 끝났으면 빈타일이 없다는 뜻
        Debug.LogWarning("TryGetEmptyTile: 빈 타일이 없습니다!");
        return false;
    }

    public bool TryGetEnemySpawnTile(bool isMelee, out Vector2Int spawnTile)
    {
        
        int yMin = -3;
        int yMax =  2;

        int xMin, xMax;

        if (isMelee)
        {
            // 근접: 앞줄 2열 (0, 1)
            xMin = 0;
            xMax = 1;
        }
        else
        {
            // 원거리: 뒷줄 2열 (4, 5)
            xMin = 4;
            xMax = 5;
        }

        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);

                if (GetTileStatus(pos) != 0)
                    continue;
                
                if (reservedTiles.ContainsKey(pos))
                    continue;

                candidates.Add(pos);
            }
        }

        // 후보가 하나도 없으면 실패
        if (candidates.Count == 0)
        {
            spawnTile = default;
            Debug.LogWarning("[TileMapManager] 적 스폰 가능한 타일이 없습니다.");
            return false;
        }

        // 후보 중 랜덤 1칸 선택
        int index = Random.Range(0, candidates.Count);
        spawnTile = candidates[index];
        
        // int before = GetTileStatus(spawnTile);
        // SetTileStatus(spawnTile, -1);
        // int after = GetTileStatus(spawnTile);

        reservedTiles[spawnTile] = int.MinValue; // 스폰용 임시 예약(오너 의미 없음)
        Debug.Log($"[SpawnTile] 선택(예약): {spawnTile}");
        return true;        
    }

    public Vector3 GetTileCenterWorld(Vector2Int tile)
    {
        // 타일 좌표를 셀 좌표로 변환
        return tilemap.GetCellCenterWorld(new Vector3Int(tile.x, tile.y, 0));
    }

    public bool IsOccupied(Vector2Int tile)
    {
        return tileToUnitId.ContainsKey(tile);
    }

    public bool IsOccupiedBy(Vector2Int tile, int unitId)
    {
        return tileToUnitId.TryGetValue(tile, out int id) && id == unitId;
    }

    // 플레이어 유닛이 점유한 게임오브젝트 반환 (없으면 null)
    public GameObject GetPlayerUnitAt(Vector2Int tile)
    {
        foreach (var go in playerUnits)
        {
            if (go == null) continue;
            var fsm = go.GetComponent<UnitFSM>();
            if (fsm != null && fsm.currentTilePosition == tile)
                return go;
        }
        return null;
    }

    public void OccupyTile(Vector2Int tile, int unitId)
    {
        // 타일 자체가 없거나(기본 상태 -1)면 점유시키면 안 됨
        if (!IsWalkable(tile))
            return;

        // 누군가 예약해 둔 스폰/이동 타일이면 "점유 확정" 시 예약은 제거
        if (reservedTiles.ContainsKey(tile))
            reservedTiles.Remove(tile);

        // 기존 점유가 있으면 덮지 않도록 방어 (원인 추적에 도움)
        if (tileToUnitId.TryGetValue(tile, out int existing) && existing != unitId)
        {
            Debug.LogWarning($"[TileMapManager] OccupyTile 충돌: {tile} 이미 {existing} 점유중, 요청={unitId}");
            return;
        }

        tileToUnitId[tile] = unitId;
        unitIdToTile[unitId] = tile;
    }

    public void VacateTile(Vector2Int tile, int unitId)
    {
        if (tileToUnitId.TryGetValue(tile, out int existing) && existing == unitId)
            tileToUnitId.Remove(tile);

        if (unitIdToTile.TryGetValue(unitId, out var t) && t == tile)
            unitIdToTile.Remove(unitId);
    }

    public void ReleaseUnitAll(int unitId)
    {
        // 점유 해제
        if (unitIdToTile.TryGetValue(unitId, out var tile))
            VacateTile(tile, unitId);

        // 예약 해제(있다면)
        // reservedTiles는 tile->ownerId 구조라 ownerId 기준으로 전부 제거
        var toRemove = new List<Vector2Int>();
        foreach (var kv in reservedTiles)
        {
            if (kv.Value == unitId)
                toRemove.Add(kv.Key);
        }
        for (int i = 0; i < toRemove.Count; i++)
            reservedTiles.Remove(toRemove[i]);
    }

    public void MoveUnitInstant(int unitId, Vector2Int newTile)
    {
        // 기존 점유 해제
        if (unitIdToTile.TryGetValue(unitId, out var oldTile))
            VacateTile(oldTile, unitId);

        // 새 타일 점유
        OccupyTile(newTile, unitId);
    }

    public void ForceMoveUnitInstant(int unitId, Vector2Int newTile)
    {
        // 1) 이 유닛이 기존에 점유하던 타일 해제
        if (unitIdToTile.TryGetValue(unitId, out var oldTile))
            VacateTile(oldTile, unitId);

        // 2) 목적지 타일을 다른 유닛이 먹고 있으면 그 유닛도 강제로 떼어냄
        if (tileToUnitId.TryGetValue(newTile, out int otherId) && otherId != unitId)
        {
            tileToUnitId.Remove(newTile);

            if (unitIdToTile.TryGetValue(otherId, out var otherTile) && otherTile == newTile)
                unitIdToTile.Remove(otherId);

            Debug.LogWarning($"[TileMap] ForceMove: kick otherId={otherId} from tile={newTile}");
        }

        // 3) 강제 점유 기록
        tileToUnitId[newTile] = unitId;
        unitIdToTile[unitId] = newTile;
    }

    public void ClearOccupancyAll()
    {
        tileToUnitId.Clear();
        unitIdToTile.Clear();

        // 예약도 꼬임 원인이면 같이
        reservedTiles.Clear();
    }

    public void RebuildOccupancyFromUnits(List<GameObject> playerUnits, List<GameObject> enemyUnits)
    {
        ClearOccupancyAll();

        void AddList(List<GameObject> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var go = list[i];
                if (go == null) continue;
                if (!go.activeInHierarchy) continue;

                var fsm = go.GetComponent<UnitFSM>();
                if (fsm == null) continue;
                Debug.Log($"[Rebuild] occupy id={fsm.unitId} tile={fsm.currentTilePosition}");
                // “현재 타일” 기준으로 확정 점유
                OccupyTile(fsm.currentTilePosition, fsm.unitId);
            }
        }

        AddList(playerUnits);
        AddList(enemyUnits);
    }

}
    


