using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public enum NodeType
{
    Combat,  //각각 등장확률은 MapGenerator.cs
    Rest,    
    Boss,    // 보스    10레벨에 보스,이후 10레벨마다 보스
    Event,   
    Trade,   
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

    private bool isInteractable = false; // 선택불가능한 노드만들기

     private SpriteRenderer spriteRenderer;  // 노드의 시각적 구분을 위해 사용

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
            Debug.Log($"Node at level {Level} clicked!");
            // MapGenerator에 노드가 클릭되었음을 알림
            FindObjectOfType<MapGenerator>().OnNodeClicked(this);
        }
        else
        {
            Debug.Log("This node is not interactable.");
        }
    }

    public void Initialize(NodeType type, int level, int index, GameObject nodeObject)
    {
        Type = type;
        Level = level;
        Index = index;
        NodeObject = nodeObject;
        Connections = new List<MapNode>();
    }

    public void ConnectTo(MapNode otherNode, Line line)
    {
        Connections.Add(otherNode);
        lines.Add(line);
    }

    // 노드의 활성화/비활성화 상태를 변경하는 메서드
    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;

        // 노드의 색상을 변경하여 활성화/비활성화 상태 시각적으로 구분
        if (spriteRenderer != null)
        {
            spriteRenderer.color = interactable ? Color.white : Color.gray;  // 활성화된 노드는 흰색, 비활성화된 노드는 회색
        }                
    }

    public void MarkAsClicked()
    {
        IsClicked = true;
        SetInteractable(false); // 클릭되면 비활성화
    }
}
