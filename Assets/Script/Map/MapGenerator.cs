using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    public int[] realBossLevel = { 8, 25, 55, 95, 145, 200 }; //네크로맨서, 200라운드 최종보스인데 미구현
    private bool IsBiomeLeaderLevel(int level) =>
        level >= biomeLeaderInterval 
        && level <= lastBiomeLeader 
        && level % biomeLeaderInterval == 0;

    private bool IsRealBossLevel(int level) =>
        Array.IndexOf(realBossLevel, level) >= 0;
        

    public GameObject combatPrefab;
    public GameObject restPrefab;
    public GameObject bossPrefab;
    public GameObject startPrefab;
    public GameObject eventPrefab;
    public GameObject tradePrefab;
    public GameObject linePrefab;
    [SerializeField] private RectTransform currentNodeCursor;
    private MapNode currentNode;

    private float combatProbability = 0.75f;  // 각노드들이 등장할 확률
    private float eventProbability = 0.15f;
    private float restProbability = 0.1f;

    private List<List<MapNode>> mapLevels;
    public SaveData saveData = new SaveData();  // 게임 데이터를 저장할 인스턴스
    
    public GameObject scrollViewContent;
    public GameObject mapScrollView; // 맵을 열고닫게끔 제어하기 위해
    public GameObject HeroScrollView;

    private bool restoredFromSave = false;

    void Start()
    {
        if (scrollViewContent == null)
        {
            Debug.LogError("ScrollViewContent is not assigned in the inspector.");
            return;
        }

        bool canRestore =
            SaveManager.instance != null &&
            SaveManager.instance.saveData != null &&
            SaveManager.instance.saveData.isValid &&
            SaveManager.instance.saveData.mapNodes != null &&
            SaveManager.instance.saveData.mapNodes.Count > 0;

        if (canRestore)
        {
            // 기존 세이브 기반으로 맵을 복원
            saveData = SaveManager.instance.saveData;
            totalLevels = Mathf.Max(totalLevels, GetMaxLevelInSave(saveData));
            AdjustContentSizeAndPosition();
            RestoreFromSave(saveData);
            restoredFromSave = true;
            Debug.Log("[MapGenerator] Start: restored map from save");
            ReopenMapViewImmediately();
        }
        else
        {
            AdjustContentSizeAndPosition();
            GenerateNodes();
            CreateConnections();
            SetNodeInteractableStates(0); // 첫 번째 레벨의 노드 활성화
            Debug.Log("[MapGenerator] Start: generated new map");
        }
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

        // 181~199라운드: 이벤트 노드 생성 금지 (전투/휴식만)
        if (level >= 181 && level <= 199)
        {
            NodeType[] nodeTypes = { NodeType.Combat, NodeType.Rest };
            float total = combatProbability + restProbability;

            // 혹시 확률 합이 0인 경우 방어
            if (total <= 0f)
                return NodeType.Combat;

            float[] probabilities = { combatProbability / total, restProbability / total };
            return GetRandomNodeTypeByProbability(nodeTypes, probabilities);
        }


        if (level < 4)  // 게임 시작하자 불필요한 휴식, 거래가 나오지 않고 레벨 5부터 나오게 제어
        {
            NodeType[] nodeTypes = { NodeType.Combat, NodeType.Event };
            float[] probabilities = { combatProbability, eventProbability };

            return GetRandomNodeTypeByProbability(nodeTypes, probabilities);
        }
        /*
        if (previousNodeType == NodeType.Trade || previousNodeType == NodeType.Rest)
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

    private int GetMaxLevelInSave(SaveData data)
    {
        int max = 0;
        if (data == null || data.mapNodes == null) return max;
        foreach (var n in data.mapNodes)
            if (n.level > max) max = n.level;
        return max;
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
            // 0라운드는 시작 노드 하나만 중앙(인덱스 2)에 생성해 다음 라운드로만 이어지게 한다.
            if (level == 0)
            {
                int nodeIndex = nodesPerLevel / 2; // 5개 기준 중앙 = 2
                GameObject nodePrefab = startPrefab != null ? startPrefab : GetPrefabForNodeType(NodeType.Combat);

                Vector2 nodePosition = new Vector2(
                    (level * iconIntervalX) - contentHalfWidth + xOffset,
                    (-nodeIndex * iconIntervalY) + contentHalfHeight - yOffset
                );

                GameObject nodeObject = Instantiate(nodePrefab, scrollViewContent.transform);
                nodeObject.name = $"Node_{level}_{nodeIndex}";

                RectTransform nodeRect = nodeObject.GetComponent<RectTransform>();
                nodeObject.GetComponent<RectTransform>().anchoredPosition = nodePosition;

                MapNode mapNode = nodeObject.GetComponent<MapNode>();
                if (mapNode == null)
                    mapNode = nodeObject.AddComponent<MapNode>();
                mapNode.Initialize(NodeType.Combat, level, nodeIndex, nodeObject);

                NodeData nodeData = new NodeData
                {
                    level = level,
                    index = nodeIndex,
                    type = NodeType.Combat,
                    connectedIndices = new List<int>()
                };
                saveData.mapNodes.Add(nodeData);

                mapLevels.Add(new List<MapNode> { mapNode });
                continue;
            }

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

    public void FillSaveData(SaveData target)
    {
        if (target == null) return;
        target.mapNodes = new List<NodeData>();
        Debug.Log($"[MapGenerator] FillSaveData start (mapLevels={mapLevels?.Count ?? 0})");

        // 현재 실제 맵 상태를 기반으로 스냅샷 생성
        for (int lvl = 0; lvl < mapLevels.Count; lvl++)
        {
            foreach (var node in mapLevels[lvl])
            {
                if (node == null) continue;
                var data = new NodeData
                {
                    level = node.Level,
                    index = node.Index,
                    type = node.Type,
                    connectedIndices = new List<int>(),
                    isClicked = node.IsClicked,
                    isCurrent = (node == currentNode),
                    isResolved = node.IsResolved,
                    resolvedEventId = node.EventId
                };

                if (node.Connections != null)
                {
                    foreach (var c in node.Connections)
                    {
                        if (c == null) continue;
                        data.connectedIndices.Add(c.Index);
                    }
                }

                target.mapNodes.Add(data);
            }
        }

        // 내부 saveData도 최신 상태로 유지
        saveData.mapNodes = new List<NodeData>(target.mapNodes);

        if (currentNode != null)
        {
            target.currentNodeLevel = currentNode.Level;
            target.currentNodeIndex = currentNode.Index;
        }
        else
        {
            target.currentNodeLevel = 0;
            target.currentNodeIndex = 0;
        }

        Debug.Log($"[MapGenerator] FillSaveData done: nodes={target.mapNodes.Count}, current=({target.currentNodeLevel},{target.currentNodeIndex})");
    }

    // currentNode가 null일 때, 진행중인 라운드 또는 가장 최근 클릭된 노드를 기준으로 복구
    public void EnsureCurrentNode(int runCurrentLevel = 0)
    {
        if (currentNode != null) return;
        MapNode candidate = null;

        // 0) 내부 saveData에 isCurrent 표시가 있으면 우선 사용
        if (saveData != null && saveData.mapNodes != null)
        {
            var cur = saveData.mapNodes.Find(n => n.isCurrent);
            if (cur != null && mapLevels != null && cur.level >= 0 && cur.level < mapLevels.Count)
            {
                candidate = mapLevels[cur.level].Find(n => n != null && n.Index == cur.index);
            }
        }

        // 1) 현재 라운드(level)에 클릭된 노드가 있으면 그것을 선택
        if (candidate == null && mapLevels != null && runCurrentLevel >= 0 && runCurrentLevel < mapLevels.Count)
        {
            foreach (var n in mapLevels[runCurrentLevel])
            {
                if (n != null && n.IsClicked)
                {
                    candidate = n;
                    break;
                }
            }
        }

        // 2) 없다면 가장 높은 레벨의 클릭된 노드를 찾음
        if (candidate == null && mapLevels != null)
        {
            for (int lvl = mapLevels.Count - 1; lvl >= 0; lvl--)
            {
                foreach (var n in mapLevels[lvl])
                {
                    if (n != null && n.IsClicked)
                    {
                        candidate = n;
                        break;
                    }
                }
                if (candidate != null) break;
            }
        }

        // 3) 그래도 없으면 스타트 노드
        if (candidate == null && mapLevels != null && mapLevels.Count > 0 && mapLevels[0].Count > 0)
        {
            candidate = mapLevels[0][0];
        }

        if (candidate != null)
        {
            currentNode = candidate;
            currentNode.SetAsCurrent();
        }
    }

    private void SyncNodeStatesIntoSaveData()
    {
        if (mapLevels == null || saveData == null || saveData.mapNodes == null) return;

        foreach (var levelNodes in mapLevels)
        {
            foreach (var node in levelNodes)
            {
                var data = saveData.mapNodes.Find(n => n.level == node.Level && n.index == node.Index);
                if (data == null) continue;

                data.isClicked = node.IsClicked;
                data.isCurrent = (node == currentNode);
                data.isResolved = node.IsResolved;
                data.resolvedEventId = node.EventId;
            }
        }
    }

    private void RestoreFromSave(SaveData data)
    {
        mapLevels = new List<List<MapNode>>();
        float iconIntervalX = 250.0f;
        float iconIntervalY = 175.0f;

        float contentHalfWidth = scrollViewContent.GetComponent<RectTransform>().sizeDelta.x / 2;
        float contentHalfHeight = scrollViewContent.GetComponent<RectTransform>().sizeDelta.y / 2;

        float xOffset = 80.0f; 
        float yOffset = 60.0f;

        // 그룹화
        var levelLookup = new Dictionary<int, List<NodeData>>();
        foreach (var nd in data.mapNodes)
        {
            if (!levelLookup.ContainsKey(nd.level))
                levelLookup[nd.level] = new List<NodeData>();
            levelLookup[nd.level].Add(nd);
        }

        // 생성
        for (int level = 0; level <= GetMaxLevelInSave(data); level++)
        {
            var levelList = new List<MapNode>();
            if (levelLookup.TryGetValue(level, out var nodeDatas))
            {
                foreach (var nd in nodeDatas)
                {
                    GameObject nodePrefab = level == 0
                        ? (startPrefab != null ? startPrefab : GetPrefabForNodeType(nd.type))
                        : GetPrefabForNodeType(nd.type);

                    Vector2 nodePosition = new Vector2(
                        (nd.level * iconIntervalX) - contentHalfWidth + xOffset,
                        (-nd.index * iconIntervalY) + contentHalfHeight - yOffset
                    );

                    GameObject nodeObject = Instantiate(nodePrefab, scrollViewContent.transform);
                    nodeObject.name = $"Node_{nd.level}_{nd.index}";
                    nodeObject.GetComponent<RectTransform>().anchoredPosition = nodePosition;

                    MapNode mapNode = nodeObject.GetComponent<MapNode>();
                    if (mapNode == null)
                        mapNode = nodeObject.AddComponent<MapNode>();
                    mapNode.Initialize(nd.type, nd.level, nd.index, nodeObject);
                    if (nd.isResolved && !string.IsNullOrEmpty(nd.resolvedEventId))
                    {
                        mapNode.ResolveEventId(nd.resolvedEventId);
                    }

                    levelList.Add(mapNode);
                }
            }
            mapLevels.Add(levelList);
        }

        // 연결 복원
        for (int level = 1; level < mapLevels.Count; level++)
        {
            var prevNodes = mapLevels[level - 1];
            var currNodes = mapLevels[level];

            foreach (var prevNode in prevNodes)
            {
                var prevData = data.mapNodes.Find(n => n.level == prevNode.Level && n.index == prevNode.Index);
                if (prevData == null || prevData.connectedIndices == null) continue;

                foreach (var idx in prevData.connectedIndices)
                {
                    var target = currNodes.Find(n => n.Index == idx);
                    if (target == null) continue;
                    DrawConnection(prevNode.NodeObject, target.NodeObject);
                }
            }
        }

        // 상태 복원
        currentNode = null;
        foreach (var levelNodes in mapLevels)
        {
            foreach (var node in levelNodes)
            {
                var nd = data.mapNodes.Find(n => n.level == node.Level && n.index == node.Index);
                if (nd == null) continue;

                if (nd.isClicked)
                    node.MarkAsClicked();

                if (nd.isCurrent)
                {
                    node.SetAsCurrent();
                    currentNode = node;
                }
            }
        }

        if (currentNode == null && mapLevels.Count > 0 && mapLevels[0].Count > 0)
        {
            currentNode = mapLevels[0][0];
            currentNode.SetAsCurrent();
        }

        ApplyVisualStateAfterRestore(currentNode);

        int nextLevel = (currentNode != null) ? currentNode.Level + 1 : 0;
        SetNodeInteractableStates(nextLevel);
    }

    private void ApplyVisualStateAfterRestore(MapNode cur)
    {
        int curLevel = cur != null ? cur.Level : 0;

        foreach (var levelNodes in mapLevels)
        {
            foreach (var node in levelNodes)
            {
                if (node == null) continue;

                if (node == cur)
                {
                    node.SetAsCurrentVisited(); // 방문색 + 하이라이트 유지
                    continue;
                }

                if (node.IsClicked)
                {
                    node.SetAsVisited();
                    continue;
                }

                // 현재 레벨 이하에서 선택되지 않은 노드는 잠금(검정)
                if (node.Level <= curLevel)
                {
                    node.SetAsLocked();
                    continue;
                }

                node.SetInteractable(false);
            }
        }

        if (currentNodeCursor != null && cur != null)
        {
            PlaceCursor(cur);
        }
    }

    void CreateConnections()
    {
        for (int level = 1; level <= totalLevels; level++) 
        {
            List<MapNode> currentLevelNodes = mapLevels[level];
            List<MapNode> previousLevelNodes = mapLevels[level - 1];

            // 0레벨 시작 노드가 하나만 있을 때는 1레벨의 모든 노드와 연결
            if (level == 1 && previousLevelNodes.Count == 1 && previousLevelNodes[0].Level == 0)
            {
                MapNode startNode = previousLevelNodes[0];
                foreach (MapNode nextNode in currentLevelNodes)
                {
                    DrawConnection(startNode.NodeObject, nextNode.NodeObject);

                    NodeData prevNodeData = saveData.mapNodes.Find(node => node.level == startNode.Level && node.index == startNode.Index);
                    NodeData nextNodeData = saveData.mapNodes.Find(node => node.level == nextNode.Level && node.index == nextNode.Index);

                    if (prevNodeData != null && nextNodeData != null && !prevNodeData.connectedIndices.Contains(nextNodeData.index))
                    {
                        prevNodeData.connectedIndices.Add(nextNodeData.index);
                    }
                }
                continue;
            }

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
                            // 다음 노드 후보
                            List<int> candidateIndices = new List<int>();
                            for (int i = minY; i <= maxY; i++)
                            {
                                candidateIndices.Add(i);
                            }

                            // 몇개로 뻗을지 결정 (1~2개)
                            int maxConnectionsPerNode = 2;
                            int available = candidateIndices.Count;
                            int connectionCount = UnityEngine.Random.Range(1, Math.Min(maxConnectionsPerNode, available) + 1);
                            
                            for (int c = 0; c < connectionCount; c++)
                            {
                                if (candidateIndices.Count == 0)
                                    break;

                                int randomIdx = UnityEngine.Random.Range(0, candidateIndices.Count);
                                int targetIndex = candidateIndices[randomIdx];
                                candidateIndices.RemoveAt(randomIdx); // 중복방지

                                MapNode nextNode = currentLevelNodes[targetIndex];
                                DrawConnection(prevNode.NodeObject, nextNode.NodeObject);

                                // 연결 정보 저장
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
        if (mapLevels == null || mapLevels.Count == 0) return;
        if (level >= mapLevels.Count) level = mapLevels.Count - 1;

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
        if (currentNode != null)
            currentNode.SetAsVisited();

        currentNode = clickedNode;
        currentNode.SetAsCurrent();

        if (saveData != null && saveData.mapNodes != null)
        {
            var prevData = saveData.mapNodes.Find(n => n.isCurrent);
            if (prevData != null) prevData.isCurrent = false;
            var nowData = saveData.mapNodes.Find(n => n.level == clickedNode.Level && n.index == clickedNode.Index);
            if (nowData != null)
            {
                nowData.isCurrent = true;
                nowData.isClicked = true;
            }
        }

        // 3) 커서를 이 노드 아래로 붙이고 위치 0, -80
        PlaceCursor(clickedNode);

        // 클릭된 노드의 현재 레벨 비활성화
        foreach (MapNode node in mapLevels[clickedNode.Level])
        {
            node.SetInteractable(false);
            node.SetAsLocked();
        }

        // 다음 레벨의 연결된 노드만 활성화
        foreach (MapNode connectedNode in clickedNode.Connections)
        {
            connectedNode.SetInteractable(true);
            connectedNode.SetAsSelectable();
        }

        // 노드의 클릭 상태 업데이트
        clickedNode.MarkAsClicked();

        // 다음 노드 상태 설정
        SetNodeInteractableStates(clickedNode.Level + 1);

        //런매니저에 선택된 노드 보내주기
        RunManager.Instance.SelectNode(clickedNode);
    }

    private void PlaceCursor(MapNode target)
    {
        if (currentNodeCursor == null || target == null) return;

        currentNodeCursor.gameObject.SetActive(true);
        currentNodeCursor.SetParent(target.NodeObject.transform, false);

        var rt = currentNodeCursor.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(0f, -80f);
            StartCoroutine(ApplyCursorOffsetNextFrame(rt));

            var cursor = currentNodeCursor.GetComponent<NodeCursor>();
            cursor?.SetBaseFromCurrent();
        }
    }

    private IEnumerator ApplyCursorOffsetNextFrame(RectTransform rt)
    {
        yield return null; // wait one frame so layout settles
        if (rt != null)
            rt.anchoredPosition = new Vector2(0f, -80f);
    }

    public void ToggleMapView()
    {
        if (mapScrollView != null)
        {
            bool isActive = mapScrollView.activeSelf;
            mapScrollView.SetActive(!isActive);  // 현재 상태를 반전시켜서 맵을 열거나 닫음
        }
    }

    public void MapViewOn()
    {
        if (mapScrollView != null)
        {
            mapScrollView.SetActive(true);
        }
    }

    public void MapViewOff()
    {
        if (mapScrollView != null)
        {
            mapScrollView.SetActive(false);
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

    private void ReopenMapViewImmediately()
    {
        if (mapScrollView == null) return;
        bool wasActive = mapScrollView.activeSelf;
        mapScrollView.SetActive(false);
        mapScrollView.SetActive(true);
        if (!wasActive)
            mapScrollView.SetActive(false);
    }
}
