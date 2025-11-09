using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]

public class NodeData
{
    public int level;                // 노드가 속한 레벨
    public int index;                // 노드의 인덱스 (레벨 내에서의 위치)
    public NodeType type;            // 노드의 타입 (전투, 휴식 등)
    public List<int> connectedIndices;  // 연결된 노드들의 인덱스
}
public class SaveData
{
    public List<NodeData> mapNodes = new List<NodeData>(); // 노드 데이터 리스트
    public int currentLevel; // 현재 레벨
    public int playerPosition; // 플레이어의 현재 위치, 0~4로 표현 가능
    public int gold;        // 플레이어가 가지고 있는 골드
    public int hp;          // hp
       
}