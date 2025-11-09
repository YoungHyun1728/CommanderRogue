using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AStarPathfinder : MonoBehaviour
{
    private class Node
    {
        public Vector2Int Position; // 노드의 타일맵 좌표
        public Node Parent; // 경로 추적을 위한 부모 노드
        public float G; // 시작 노드에서 현재 노드까지의 실제 비용
        public float H; // 현재 노드에서 목표 노드까지의 휴리스틱 비용
        public float F => G + H; // 총 비용 (우선 순위 결정)

        public TileMapManager tileMapManager;

        public Node(Vector2Int position, Node parent, float g, float h)
        {
            Position = position;
            Parent = parent;
            G = g;
            H = h;
        }
    }

    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int target, TileMapManager tileMapManager, HashSet<Vector2Int> occupiedTiles)
    {
        // 현재 타일을 점유된 타일에서 제외
        occupiedTiles.Remove(start);

        List<Vector2Int> targetNeighbors = GetNeighbors(target, tileMapManager, occupiedTiles);
        if (targetNeighbors.Count == 0)
        {
            Debug.LogWarning("타겟 주변에 이동 가능한 타일이 없습니다.");
            return new List<Vector2Int>();
        }

        // 타겟의 인접 타일들 중 가장 가까운 타일을 실제 목표로 설정
        Vector2Int closestNeighbor = targetNeighbors[0];
        float minDistance = Vector2Int.Distance(start, closestNeighbor);

        foreach (var neighbor in targetNeighbors)
        {
            float distance = Vector2Int.Distance(start, neighbor);
            if (distance < minDistance)
            {
                closestNeighbor = neighbor;
                minDistance = distance;
            }
        }

        // 새 목표 타일로 경로 계산
        return FindPathInternal(start, closestNeighbor, tileMapManager, occupiedTiles);
    }

    public static List<Vector2Int> FindPathInternal(Vector2Int start, Vector2Int goal, TileMapManager tileMapManager, HashSet<Vector2Int> occupiedTiles)
    {
        // TileMapManager 유효성 검사
        if (tileMapManager == null)
        {
            Debug.LogError("TileMapManager가 null입니다. 경로 탐색을 진행할 수 없습니다.");
            return new List<Vector2Int>();
        }

        // Open List와 Closed List 초기화
        var openList = new List<Node>();
        var closedList = new HashSet<Vector2Int>();

        // 시작 노드를 Open List에 추가
        openList.Add(new Node(start, null, 0, Heuristic(start, goal)));

        while (openList.Count > 0)
        {
            // Open List에서 f 값이 가장 낮은 노드를 선택
            Node currentNode = GetLowestCostNode(openList);

            // 목표 지점에 도달하면 경로 반환
            if (currentNode.Position == goal)
            {
                return ReconstructPath(currentNode);
            }

            // 현재 노드를 Open List에서 제거하고 Closed List에 추가
            openList.Remove(currentNode);
            closedList.Add(currentNode.Position);

            // 현재 노드의 이웃 노드를 탐색
            foreach (var neighbor in GetNeighbors(currentNode.Position, tileMapManager, occupiedTiles))
            {
                // 이미 탐색한 노드는 무시
                if (closedList.Contains(neighbor))
                {   
                    continue;
                }

                // g 값 계산 (현재 노드까지의 비용 + 1)
                float g = currentNode.G + 1;

                // Open List에 없는 노드면 추가
                Node neighborNode = openList.Find(n => n.Position == neighbor);
                if (neighborNode == null)
                {
                    openList.Add(new Node(neighbor, currentNode, g, Heuristic(neighbor, goal)));
                }
                // 이미 Open List에 있는 노드라면 더 나은 경로인지 확인
                else if (g < neighborNode.G)
                {
                    neighborNode.G = g;
                    neighborNode.Parent = currentNode;
                }
            }
        }

        // 경로를 찾을 수 없으면 빈 리스트 반환
        return new List<Vector2Int>();
    }

    private static List<Vector2Int> ReconstructPath(Node node)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        while (node != null)
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse();
        return path;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

     private static Node GetLowestCostNode(List<Node> openList)
    {
        Node lowestCostNode = openList[0];
        foreach (var node in openList)
        {
            if (node.F < lowestCostNode.F)
            {
                lowestCostNode = node;
            }
        }
        return lowestCostNode;
    }

    private static List<Vector2Int> GetNeighbors(Vector2Int position, TileMapManager tileMapManager, HashSet<Vector2Int> occupiedTiles)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        // 상하좌우 타일 확인
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        BoundsInt bounds = tileMapManager.tilemap.cellBounds;
        
        foreach (var direction in directions)
        {
            Vector2Int neighborPosition = position + direction;

            // 타일맵 경계 확인
            if (!bounds.Contains(new Vector3Int(neighborPosition.x, neighborPosition.y, 0)))
            {
                continue;
            }

             // 이동 가능한 타일만 추가, 점유된 타일 제외
            if (tileMapManager.IsWalkable(neighborPosition) && !occupiedTiles.Contains(neighborPosition))
            {
                neighbors.Add(neighborPosition);
            }
        }

        // 자신이 있는 위치를 추가
        if (!occupiedTiles.Contains(position))
        {
            neighbors.Add(position);
        }
        
        return neighbors;
    }

    private static Vector2Int GetClosestNeighbor(Vector2Int target, TileMapManager tileMapManager, HashSet<Vector2Int> occupiedTiles)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        List<Vector2Int> neighbors = new List<Vector2Int>();

        foreach (var direction in directions)
        {
            Vector2Int neighbor = target + direction;
            if (tileMapManager.IsWalkable(neighbor) && !occupiedTiles.Contains(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        if (neighbors.Count > 0)
        {
            // 적의 타일에 가장 가까운 타일 반환
            return neighbors[0]; // 또는 가장 가까운 논리를 추가 가능
        }
        else
        {
            // 갈 수 있는 타일이 없으면 그대로 반환
            return target;
        }
    }
}
