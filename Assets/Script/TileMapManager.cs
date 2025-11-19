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

    public Vector2Int tilemapOrigin; // 타일맵의 (0,0)
    
    // 테스트용 배열
    public GameObject[] unitPrefab = new GameObject[2]; // 인스펙터에서 유닛 프리팹 할당 / 테스트용
    void Start()
    {
        InitializeTileStatus();

        if (unitPrefab == null)
        {
            Debug.LogError("unitPrefab이 인스펙터에 할당되지 않았습니다!");
            return;
        }
        
        // 첫 번째 유닛 생성 및 초기화
        GameObject unit1 = Instantiate(unitPrefab[0]);
        unit1.GetComponent<UnitFSM>().Initialize(this, new Vector2Int(-6, -3));
        playerUnits.Add(unit1);

        // 두 번째 유닛 생성 및 초기화
        GameObject unit2 = Instantiate(unitPrefab[2]);
        unit2.GetComponent<UnitFSM>().Initialize(this, new Vector2Int(-1, 2));
        playerUnits.Add(unit2);

        Debug.Log("두 유닛 생성 테스트 완료");

        GameObject unit3 = Instantiate(unitPrefab[1]);
        unit3.GetComponent<UnitFSM>().Initialize(this, new Vector2Int(5, -1));
        enemyUnits.Add(unit3);

        GameObject unit4 = Instantiate(unitPrefab[1]);
        unit4.GetComponent<UnitFSM>().Initialize(this, new Vector2Int(5, 2));
        enemyUnits.Add(unit4);

        Debug.Log("적 유닛 생성 테스트 완료");
        
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
        // 모든 타일 초기화
        for (int i = 0; i < tileDataList.Count; i++)
        {
            tileDataList[i] = new TileData(tileDataList[i].Position, 0); // 기본상태 : 비어있음 [0]
        }
        
        // 플레이어 유닛의 위치를 -1로 설정
        foreach (var unit in playerUnits)
        {
            UnitFSM unitFsm = unit.GetComponent<UnitFSM>();
            if (unitFsm != null)
            {
                SetTileStatus(unitFsm.currentTilePosition, -1);
            }
        }

        // 적 유닛의 위치를 -1로 설정
        foreach (var unit in enemyUnits)
        {
            UnitFSM unitFsm = unit.GetComponent<UnitFSM>();
            if (unitFsm != null)
            {
                SetTileStatus(unitFsm.currentTilePosition, -1);
            }
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
                return tileData.Status;
            }
        }

        Debug.LogWarning($"타일 {position}이 존재하지 않습니다.");
        return -1; // 기본값: 이동 불가
    }

    // 이동가능한 타일인지 확인
    public bool IsWalkable(Vector2Int tilePosition)
    {
        int status = GetTileStatus(tilePosition);
        if(status != -1)
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

    public void PlaceCharacter(Vector2Int tilePosition)
    {
        int status = GetTileStatus(tilePosition);

        if (status == 0) // 비어 있는 타일이면 캐릭터 배치
        {
            status = -1;
            Debug.Log("캐릭터가 배치되었습니다: " + tilePosition);
            Vector3Int setTilePosition = new Vector3Int(tilePosition.x, tilePosition.y, 0);
            tilemap.SetTile(setTilePosition, null); // 하이라이트 제거
        }
        else
        {
            Debug.Log("타일에 이미 다른 오브젝트가 있습니다.");
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

    // 이동이 끝났거나 취소될 때 예약 해제
    public void ReleaseReservedTile(Vector2Int tile, int unitId)
    {
        if (reservedTiles.TryGetValue(tile, out int ownerId) && ownerId == unitId)
        {
            reservedTiles.Remove(tile);
        }
    }

}
