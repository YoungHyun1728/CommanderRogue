using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;

public enum NodeType
{
    Combat,  //각각 등장확률은 MapGenerator.cs
    Rest,    // 전체 체력회복 
    Boss,    // 보스    정해진 레벨마다 등장
    Necromancer, // 네크로맨서 라운드 정해진 레벨마다 등장
    Event    // 이벤트 몇몇 이벤트 추가예정
}

public class MapNode : MonoBehaviour
{
    public NodeType Type { get; private set; }
    public int Level { get; private set; }
    public int Index { get; private set; }  // 노드의 인덱스 (레벨 내에서의 위치)

    public List<MapNode> Connections { get; private set; }  // 연결된 노드들
    public GameObject NodeObject { get; private set; }

    public bool IsClicked { get; private set; } = false;
    public List<Line> lines = new List<Line>();
    public List<GameObject> prevNodePrefab = new List<GameObject>();

    private bool isInteractable = false; // 활성화 여부    
    private bool isResolved;
    private string resolvedEventId;
    public bool IsResolved => isResolved;
    public string EventId => resolvedEventId;

    private Image img;  // 노드의 시각적 구분을 위해 사용

    [Header("Node Colors")]
    [SerializeField] private Color currentColor = Color.white;      // 기본 색
    [SerializeField] private Color selectableColor = Color.white;   // 선택 가능
    [SerializeField] private Color lockedColor = Color.black;       // 잠김/무의미
    [SerializeField] private Color visitedColor = Color.gray;       // 이미 지난 노드
    [SerializeField] private GameObject currentHighlight;           // 현재 위치 강조 표시

    void Awake()
    {
        img = GetComponent<Image>();
        if (currentHighlight != null)
            currentHighlight.SetActive(false);
    }

    private void Update()
    {
        if (Level == 0)  // 레벨 0은 파괴하지 않음
        {
            return;
        }

        if (prevNodePrefab == null || prevNodePrefab.Count == 0)
        {
            Destroy(gameObject); // 리스트가 null이거나 비어있을 때 파괴
        }
        else
        {
            bool allNoneOrMissing = true;  // 모든 노드가 None(또는 Missing)인지 확인하는 변수

            foreach (var prevNode in prevNodePrefab)
            {
                if (prevNode != null)  // null(즉, None 상태)이 아닌 노드가 있으면
                {
                    allNoneOrMissing = false;
                    break;  // 더 이상 체크할 필요 없음
                }
            }

            if (allNoneOrMissing)
            {
                Destroy(gameObject);   // 모든 노드가 None(또는 Missing)일 경우 파괴
            }
        }
    }

    public void OnNodeClicked()
    {
        if (isInteractable)
        {
            Debug.Log($"{Level} 라운드 선택! - {Type} 라운드");
            // MapGenerator에 노드가 클릭되었음을 알림
            FindObjectOfType<MapGenerator>().OnNodeClicked(this);
        }
        else
        {
            Debug.Log("선택할 수 없는 노드입니다!!");
        }
    }

    public void Initialize(NodeType type, int level, int index, GameObject nodeObject)
    {
        Type = type;
        Level = level;
        Index = index;
        NodeObject = nodeObject;
        Connections = new List<MapNode>();
        isResolved = false;
        resolvedEventId = "";
    }

    public void ConnectTo(MapNode otherNode, Line line)
    {
        Connections.Add(otherNode);
        lines.Add(line);
    }
    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
    }

    public void MarkAsClicked()
    {
        IsClicked = true;
        SetAsVisited();
    }

    public void SetAsCurrent()
    {
        isInteractable = false;  // 이미 선택된 노드는 다시 못 누르게
        if (img != null)
            img.color = currentColor;

        if (currentHighlight != null)
            currentHighlight.SetActive(true);
    }

    public void SetAsSelectable()
    {
        isInteractable = true;
        if (img != null)
            img.color = selectableColor;

        if (currentHighlight != null)
            currentHighlight.SetActive(false);
    }

    public void SetAsLocked()
    {
        isInteractable = false;
        if (img != null)
            img.color = lockedColor;

        if (currentHighlight != null)
            currentHighlight.SetActive(false);
    }

    public void SetAsVisited()
    {
        isInteractable = false;
        if (img != null)
            img.color = visitedColor;

        if (currentHighlight != null)
            currentHighlight.SetActive(false);
    }

    public void ResolveEventId(string id)
    {
        isResolved = true;
        resolvedEventId = id;
    }
}
