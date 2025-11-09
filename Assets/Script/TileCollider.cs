using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileCollider : MonoBehaviour
{
    public Vector2Int Position; // 타일 좌표

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"[TileCollider] Enter 타일: {Position}");
        // 유닛이 해당 타일에 들어왔을 때 처리 로직
        TileMapManager tileMapManager = FindObjectOfType<TileMapManager>();
        tileMapManager.SetTileStatus(Position, -1); // 점유 상태로 설정
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Debug.Log($"[TileCollider] Exit 타일: {Position}");
        // 유닛이 해당 타일에서 나갔을 때 처리 로직
        TileMapManager tileMapManager = FindObjectOfType<TileMapManager>();
        tileMapManager.SetTileStatus(Position, 0); // 비어 있음으로 설정
    }
}
