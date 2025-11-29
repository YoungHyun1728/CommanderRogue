using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 런 상태 관리 클래스
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }    
    public int currentLevel; // 현재 진행중인 라운드
    public double gold; // 이벤트나 상점에서 사용되는 재화
    public List<GameObject> playerUnits = new List<GameObject>(); // 플레이어 캐릭터 리스트
    public MapGenerator mapGenerator; //  MapGenerator 참조
    // 2) 현재 선택된 노드 정보 (MapGenerator에서 넘겨줌)
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
        
    }

    void StartNewRun()
    {
        currentLevel = 1;
        gold = 1000; // 초기 골드 설정
        playerUnits.Clear();
        isInBattle = false;
        isInEvent = false;
        isInReward = false;
        //튜토리얼 기능 추가시 작성

        // 기본 유닛 하나 추가

        // 맵생성 함수 호출
    }

    void SelectNode(NodeType nodeType)
    {
        currentNodeType = nodeType;
        // 선택된 노드에 따라 이벤트 처리
        switch (nodeType)
        {
            case NodeType.Combat:
                isInBattle = true;
                // 전투 시작 로직
                break;
            case NodeType.Boss:
                isInBattle = true;
                // 보스 시작전 대화 하고 전투 시작
                break;
            case NodeType.Event:
                isInEvent = true;
                // 이벤트 시작 로직
                // 이벤트 종류에 따라 분기 처리 필요
                break;
            case NodeType.Rest:
                isInReward = true;
                // 보상 선택 로직
                break;
            default:
                break;
        }
    }

    

}
