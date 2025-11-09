using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class MapGenerator : MonoBehaviour
{
    public int totalLevels = 200;
    public int nodesPerLevel = 5;
    public int biomeLeaderInterval = 20;  // 20레벨마다 보스
    public int lastBiomeLeader = 180;
    public int[] realBossLevel = { 25, 55, 95, 145, 195 }; //진 보스 조우 레벨
    private bool IsBiomeLeaderLevel(int level) =>
        level >= biomeLeaderInterval 
        && level <= lastBiomeLeader 
        && level % biomeLeaderInterval == 0;

    private bool IsRealBossLevel(int level) =>
        Array.IndexOf(realBossLevel, level) >= 0;
        

    public GameObject combatPrefab;
    public GameObject restPrefab;
    public GameObject bossPrefab;
    public GameObject eventPrefab;
    public GameObject tradePrefab;
    public GameObject linePrefab;  

    private float combatProbability = 0.75f;  // 각노드들이 등장할 확률
    private float eventProbability = 0.15f;
    private float restProbability = 0.1f;

    private List<List<MapNode>> mapLevels;
    public SaveData saveData = new SaveData();  // 게임 데이터를 저장할 인스턴스
    
    public GameObject scrollViewContent;
    public GameObject mapScrollView; // 맵을 열고닫게끔 제어하기 위해
    public GameObject HeroScrollView;
    void Start()
    {
        if (scrollViewContent == null)
        {
            Debug.LogError("ScrollViewContent is not assigned in the inspector.");
            return;
        }

        AdjustContentSizeAndPosition();
        GenerateNodes();
        CreateConnections();
        SetNodeInteractableStates(0); // 첫 번째 레벨의 노드 활성화
    }

    NodeType GetRandomNodeTypeByProbability(NodeType[] nodeTypes, float[] probabilities)
    {
        float randomValue = UnityEngine.Random.value;  // 랜덤 0~1 반환시킨다.
        float cumulativeProbability = 0f;  // 확률 누적용 변수

        for (int i = 0; i < nodeTypes.Length; i++)
        {
            // 랜덤으로 반환된 value를 cumulativeProbability와 비교해서 노드 반환
            cumulativeProbability += probabilities[i];
            if (randomValue < cumulativeProbability)
            {
                return nodeTypes[i];
            }
        }

        return NodeType.Combat;
    }

    NodeType GetRandomNodeType(int level)
    {
        float randomValue = UnityEngine.Random.value;  // 0~1 사이 값 반환

        if (level == 0)                    //첫번째는 항상 전투
        {
            return NodeType.Combat;
        }

        if (IsBiomeLeaderLevel(level) || IsRealBossLevel(level))
        {
            return NodeType.Boss;
        }

        if (level < 4)  // 게임 시작하자 불필요한 휴식, 거래가 나오지 않고 레벨 5부터 나오게 제어
        {
            NodeType[] nodeTypes = { NodeType.Combat, NodeType.Event };
            float[] probabilities = { combatProbability, eventProbability };

            return GetRandomNodeTypeByProbability(nodeTypes, probabilities);
        }

        // 이전 노드가 휴식이거나 거래일 경우 연속으로 나오지 않게 조정
        // 연결만 안되면 되는 문제라 수정해야 할거같음
        /*if (previousNodeType == NodeType.Trade || previousNodeType == NodeType.Rest)
        {
            NodeType[] nodeTypes = { NodeType.Combat, NodeType.Event };
            float[] probabilities = { combatProbability, eventProbability };

            return GetRandomNodeTypeByProbability(nodeTypes, probabilities);
        }*/

        NodeType[] allNodeTypes = { NodeType.Combat, NodeType.Event, NodeType.Rest };
        float[] allProbabilities = { combatProbability, eventProbability, restProbability};

        return GetRandomNodeTypeByProbability(allNodeTypes, allProbabilities);

    }

    GameObject GetPrefabForNodeType(NodeType type)
    {
        switch (type)
        {
            case NodeType.Combat:
                return combatPrefab;
            case NodeType.Rest:
                return restPrefab;
            case NodeType.Boss:
                return bossPrefab;
            case NodeType.Event:
                return eventPrefab;
            default:
                return combatPrefab; // 기본값으로 전투노드
        }
    }

    void DrawConnection(GameObject startNode, GameObject endNode)
    {
        // 라인 프리팹으로부터 라인 오브젝트 생성
        GameObject lineObject = Instantiate(linePrefab, scrollViewContent.transform);

        // RectTransform을 통해 라인 오브젝트를 설정
        RectTransform rect = lineObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = lineObject.AddComponent<RectTransform>();
        }

        UnityEngine.UI.Image lineImage = lineObject.GetComponent<UnityEngine.UI.Image>();
        if (lineImage == null)
        {
            lineImage = lineObject.AddComponent<UnityEngine.UI.Image>();
        }

        // 라인 생성 후 위치 조정
        lineObject.transform.SetAsFirstSibling();

        // 노드의 위치를 가져와 선을 그릴 위치를 설정
        Vector3 startPos = startNode.GetComponent<RectTransform>().anchoredPosition;
        Vector3 endPos = endNode.GetComponent<RectTransform>().anchoredPosition;

        // 노드의 RectTransform 가져오기
        RectTransform startRect = startNode.GetComponent<RectTransform>();
        RectTransform endRect = endNode.GetComponent<RectTransform>();

        if (startRect == null || endRect == null)
        {
            Debug.LogError("RectTransform이 존재하지 않습니다.");
            return;
        }

        // 선의 중심을 설정
        rect.anchoredPosition = (startPos + endPos) / 2;

        // 선의 길이와 회전을 설정
        float distance = Vector3.Distance(startPos, endPos);
        rect.sizeDelta = new Vector2(distance, 5f); // 두께 5px
        rect.rotation = Quaternion.FromToRotation(Vector3.right, endPos - startPos);

         // Line 컴포넌트를 가져오거나 추가
        Line line = lineObject.GetComponent<Line>();
        if (line == null)
        {
            line = lineObject.AddComponent<Line>();
        }

        //연결 정보
        MapNode startMapNode = startNode.GetComponent<MapNode>();
        MapNode endMapNode = endNode.GetComponent<MapNode>();

        line.startNode = startNode;
        line.endNode = endNode;

        if (startMapNode != null)
        {
            startMapNode.ConnectTo(endMapNode, null); // 연결추가
        }

        if (endMapNode != null)
        {
            if (endMapNode.prevNodePrefab == null)
            {
                endMapNode.prevNodePrefab = new List<GameObject>();
            }
            endMapNode.prevNodePrefab.Add(startNode);
        }
    }

    void GenerateNodes()
    {
        mapLevels = new List<List<MapNode>>();
        float iconIntervalX = 250.0f;
        float iconIntervalY = 175.0f;

        float contentHalfWidth = scrollViewContent.GetComponent<RectTransform>().sizeDelta.x / 2;
        float contentHalfHeight = scrollViewContent.GetComponent<RectTransform>().sizeDelta.y / 2;

        float xOffset = 80.0f; 
        float yOffset = 60.0f; 

        for (int level = 0; level <= totalLevels; level++)
        {
            List<MapNode> currentLevelNodes = new List<MapNode>();

            for (int nodeIndex = 0; nodeIndex < nodesPerLevel; nodeIndex++)
            {
                NodeType nodeType = GetRandomNodeType(level);
                GameObject nodePrefab = GetPrefabForNodeType(nodeType);

                // 노드 배치
                Vector2 nodePosition = new Vector2(
                    (level * iconIntervalX) - contentHalfWidth + xOffset,  // X offset 
                    (-nodeIndex * iconIntervalY) + contentHalfHeight - yOffset  // Y offset 
                );


                // 노드생성, 이름설정
                GameObject nodeObject = Instantiate(nodePrefab, scrollViewContent.transform);
                nodeObject.name = $"Node_{level}_{nodeIndex}";

                // RectTransForm이용
                RectTransform nodeRect = nodeObject.GetComponent<RectTransform>();
                nodeObject.GetComponent<RectTransform>().anchoredPosition = nodePosition;


                MapNode mapNode = nodeObject.GetComponent<MapNode>();
                if (mapNode == null)
                {
                    mapNode = nodeObject.AddComponent<MapNode>();
                }
                mapNode.Initialize(nodeType, level, nodeIndex, nodeObject);

                // 노드 정보를 저장
                NodeData nodeData = new NodeData
                {
                    level = level,
                    index = nodeIndex,
                    type = nodeType,
                    connectedIndices = new List<int>() // 이후 연결이 만들어질 때 추가됨
                };
                saveData.mapNodes.Add(nodeData);

                currentLevelNodes.Add(mapNode);
            }

            // 보스 레벨인 경우, 3번째 노드 제외한 나머지 4개 삭제
            if (level > 0 && (IsBiomeLeaderLevel(level) || IsRealBossLevel(level)))
            {
                for (int nodeIndex = 0; nodeIndex < currentLevelNodes.Count; nodeIndex++)
                {
                    if (nodeIndex != 2) // 3번째 노드를 제외하고 나머지 삭제
                    {
                        Destroy(currentLevelNodes[nodeIndex].NodeObject);
                    }
                }
                // 3번째 노드만 남기고 currentLevelNodes를 갱신 (리스트 복사)
                List<MapNode> bossNodeList = new List<MapNode> { currentLevelNodes[2] };

                // mapLevels에 갱신된 노드 리스트 추가
                mapLevels.Add(bossNodeList);

                // currentLevelNodes를 새로운 보스 노드 리스트로 교체
                currentLevelNodes = bossNodeList;
            }
            else
            {
                mapLevels.Add(currentLevelNodes);
            }

        }
    }

    void CreateConnections()
    {
        for (int level = 1; level <= totalLevels; level++) 
        {
            List<MapNode> currentLevelNodes = mapLevels[level];
            List<MapNode> previousLevelNodes = mapLevels[level - 1];

            foreach (MapNode prevNode in previousLevelNodes)
            {
                // 보스 레벨이면 보스 노드(3번째 노드)를 다음 레벨의 모든 노드에 연결
                if (prevNode.Type == NodeType.Boss)
                {
                    foreach (MapNode nextNode in currentLevelNodes)
                    {
                        DrawConnection(prevNode.NodeObject, nextNode.NodeObject);
                        
                        // 연결 정보 SaveData에 저장
                        NodeData prevNodeData = saveData.mapNodes.Find(node => node.level == prevNode.Level && node.index == prevNode.Index);
                        NodeData nextNodeData = saveData.mapNodes.Find(node => node.level == nextNode.Level && node.index == nextNode.Index);
                        
                        if (prevNodeData != null && nextNodeData != null)
                        {
                            prevNodeData.connectedIndices.Add(nextNodeData.index);
                        }
                    }
                }
                else if (IsBiomeLeaderLevel(level) || IsRealBossLevel(level))
                {
                    // 현재 레벨에서 보스 노드가 있는지 확인
                    if (currentLevelNodes.Count > 0)
                    {
                        MapNode bossNode = currentLevelNodes[0];
                        DrawConnection(prevNode.NodeObject, bossNode.NodeObject);

                        // 연결 정보 SaveData에 저장
                        NodeData prevNodeData = saveData.mapNodes.Find(node => node.level == prevNode.Level && node.index == prevNode.Index);
                        NodeData nextNodeData = saveData.mapNodes.Find(node => node.level == bossNode.Level && node.index == bossNode.Index);
                        
                        if (prevNodeData != null && nextNodeData != null)
                        {
                            prevNodeData.connectedIndices.Add(nextNodeData.index);
                        }
                    }
                }
                else
                {
                    // 일반 레벨의 노드 연결
                    int prevIndex = previousLevelNodes.IndexOf(prevNode);
                    if (prevIndex >= 0 && prevIndex < currentLevelNodes.Count)
                    {
                        int minY = Mathf.Max(0, prevIndex - 1); // 최대 위아래 한칸씩만
                        int maxY = Mathf.Min(currentLevelNodes.Count - 1, prevIndex + 1); 

                        // 유효한 범위 내에서 노드 선택
                        if (minY <= maxY)
                        {
                            MapNode nextNode = currentLevelNodes[UnityEngine.Random.Range(minY, maxY + 1)];
                            DrawConnection(prevNode.NodeObject, nextNode.NodeObject);
                            
                            // 연결 정보를 저장
                            NodeData prevNodeData = saveData.mapNodes.Find(node => node.level == prevNode.Level && node.index == prevNode.Index);
                            NodeData nextNodeData = saveData.mapNodes.Find(node => node.level == nextNode.Level && node.index == nextNode.Index);
                            
                            if (prevNodeData != null && nextNodeData != null)
                            {
                                prevNodeData.connectedIndices.Add(nextNodeData.index);
                            }
                        }
                    }
                }
            }
        }
    }

    void AdjustContentSizeAndPosition() // contents크기 설정 및 위치조정
    {
        RectTransform contentRect = scrollViewContent.GetComponent<RectTransform>();

        float contentWidth = (totalLevels + 1) * 250.0f; 
        contentRect.sizeDelta = new Vector2(contentWidth, contentRect.sizeDelta.y);

        contentRect.localPosition = new Vector2(0, 0);
    }

    void SetNodeInteractableStates(int level)
    {
        foreach (List<MapNode> levelNodes in mapLevels)
        {
            foreach (MapNode node in levelNodes)
            {
                node.SetInteractable(false);
            }
        }

        // 첫 번째 레벨의 모든 노드를 활성화
        if (level == 0)
        {
            foreach (MapNode node in mapLevels[level])
            {
                node.SetInteractable(true);                
            }
        }
        else
        {
            // 클릭된 노드와 연결된 다음 레벨의 노드만 활성화
            foreach (MapNode node in mapLevels[level - 1])
            {
                if (node.IsClicked)
                {
                    foreach (MapNode connectedNode in node.Connections)
                    {
                        connectedNode.SetInteractable(true);
                    }
                }
            }
        }
    }

    public void OnNodeClicked(MapNode clickedNode)
    {
        // 클릭된 노드의 현재 레벨 비활성화
        foreach (MapNode node in mapLevels[clickedNode.Level])
        {
            node.SetInteractable(false);
        }

        // 다음 레벨의 연결된 노드만 활성화
        foreach (MapNode connectedNode in clickedNode.Connections)
        {
            connectedNode.SetInteractable(true);
        }

        // 노드의 클릭 상태 업데이트
        clickedNode.MarkAsClicked();

        // 다음 노드 상태 설정
        SetNodeInteractableStates(clickedNode.Level + 1);
    }

    public void ToggleMapView()
    {
        if (mapScrollView != null)
        {
            bool isActive = mapScrollView.activeSelf;
            mapScrollView.SetActive(!isActive);  // 현재 상태를 반전시켜서 맵을 열거나 닫음
        }
    }

    public void ToggleHeroTap()
    {
        if (mapScrollView != null)
        {
            bool isActive = HeroScrollView.activeSelf;
            HeroScrollView.SetActive(!isActive);  // 현재 상태를 반전시켜서 맵을 열거나 닫음
        }
    }
}