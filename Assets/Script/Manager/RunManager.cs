using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 런 상태 관리 클래스
public class RunManager : MonoBehaviour
{
    [Header("유닛 관련")]
    [SerializeField] private List<UnitData> allUnits;       // 전체 유닛 풀
    [SerializeField] private UnitSelectPanel unitSelectPanel;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private TileMapManager tileMapManager;

    public static RunManager Instance { get; private set; }
    public enum RunState
    {
        OnMap,          // 맵에서 다음 노드를 고르는 상태
        Ready,          // 전투, 이벤트 노드에서 선택지를 고르는 상태
        Battle,         // 전투중인 상태
        Reward,         // 라운드 클리어 후 보상, 상점 이용중
        Event,          // 이벤트 진행중
        Rest            // 휴식, 이것도 이벤트이긴한데 일단 넣어둠
    }
    public RunState currentRunState {get; private set;} //초기 상태
    public int currentLevel; // 현재 진행중인 라운드
    public double gold; // 이벤트나 상점에서 사용되는 재화
    public List<GameObject> playerUnits = new List<GameObject>(); // 플레이어 캐릭터 리스트
    
    public NodeType currentNodeType { get; private set; }

    public bool isInBattle; // 전투중인지 여부
    public bool isInEvent; // 이벤트 중인지 여부
    public bool isInReward; // 보상 선택 중인지 여부

    public enum WeatherType
    {
        Sunny,   // 쾌청
        Rainy,   // 비
        Snowy,   // 눈
        Windy,  // 강풍
        None    // 바람
    }

    private WeatherType currentWeather = WeatherType.None;
        
    // 싱글톤 (다른씬에 넘어갈일은 없지만 일단 만들어둠)
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        //StartNewRun();
    }

    void StartNewRun()
    {
        currentLevel = 1;
        gold = 1000; // 초기 골드 설정
        playerUnits.Clear();
        isInBattle = false;
        isInEvent = false;
        isInReward = false;
        currentRunState = RunState.OnMap; // 초기에 지도 부터 보여준다.
        //튜토리얼 기능 추가시 작성

        // 기본 유닛 하나 추가

        // 맵생성 함수 호출

        //맵열기
        mapGenerator.ToggleMapView();
    }

    public void SelectNode(MapNode node) // Map에서 노드를 클릭할때 호출
    {
        currentNodeType = node.Type;
        currentLevel = node.Level;

        // 선택된 노드에 따라 이벤트 처리
        switch (currentNodeType)
        {
            case NodeType.Combat:
                // 대기모드
                // 전투 시작 로직
                break;
            case NodeType.Boss:
                // 대기 모드
                // 보스 시작전 대화 하고 전투 노드랑 똑같이 작동
                break;
            case NodeType.Event:
                // 이벤트 시작 로직
                // 이벤트 종류에 따라 분기 처리 필요
                break;
            case NodeType.Rest:
                // 보상 선택 로직
                break;
            default:
                break;
        }
    }

    void EnterReady()
    {
        currentRunState = RunState.Ready;

        AllUnitsReady();
    }

    void StartBattle()
    {
        // 대기상태에서 전투하기 버튼 선택하면
        isInBattle = true; 
        currentRunState = RunState.Battle;

        AllUnitsIdle();
    }

    void EndBattle()
    {
        // 유닛리스트를 통해서 적리스트가 모두 없어지면 승리
        // 아군 유닛이 모두 Faint상태이면 패배
        
        // 유닛 기절시 삭제 판정 리스트로 전멸 판단
        if(tileMapManager.enemyUnits.Count == 0 && isInBattle == true)
        {
            isInBattle = false;
            EnterReward();            
        }
        else if(tileMapManager.playerUnits.Count == 0 && isInBattle == true)
        {
            isInBattle = false;
            //게임 오버
        }
    }

    void EnterReward()
    {
        isInReward = true;
        currentRunState = RunState.Reward;
        // 보상 UI 구현
        // 선택후 다음라운드 진행 구현 
        // Map 다시 열기
        isInReward = false;
    }
    
    void AllUnitsReady()
    {
        // 타일맵에 있는 유닛리스트를 통해서 모든 유닛 Ready상태로 변경
    }

    void AllUnitsIdle()
    {
        // 타일맵에 있는 유닛리스트를 통해 모든 유닛 idle상태 로 변경
    }

    // 플레이어에게 유닛을 제공하는 함수
    public void GainUnit()
    {
        List<UnitData> candidates = GetRandomUnits(3);
        unitSelectPanel.Open(candidates, OnUnitSelected);
    }

    private List<UnitData> GetRandomUnits(int count)
    {
        var list = new List<UnitData>(allUnits); // 플레이어블 유닛만 있음

        // 셔플
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }

        if (list.Count > count)
            list.RemoveRange(count, list.Count - count);

        return list;
    }

    private void OnUnitSelected(UnitData selected)
    {
        SpawnUnit(selected);
    }

    private void SpawnUnit(UnitData data)
    {
        // 프리팹 인스턴스 생성
        GameObject unit = Instantiate(data.prefab);

        // 시작 위치 (빈자리를 찾는 함수 필요)
        Vector2Int startTile;
        tileMapManager.GetEmptyTile(out startTile);

        // UnitFSM 초기화
        UnitFSM fsm = unit.GetComponent<UnitFSM>();
        fsm.Initialize(tileMapManager, startTile);

        // Unit에 UnitData 적용 (원하면)
        Unit unitComp = unit.GetComponent<Unit>();
        if (unitComp != null)
        {
            // 여기서 Unit 쪽에 ApplyData(UnitData) 함수 만들어서 호출
        }

        // 런/타일맵에 등록
        playerUnits.Add(unit);
        tileMapManager.playerUnits.Add(unit);
    }
   

}
