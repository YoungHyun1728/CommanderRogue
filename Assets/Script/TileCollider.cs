using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileCollider : MonoBehaviour
{
    public Vector2Int Position; // 타일 좌표
    private TileMapManager tileMapManager;

    private void Awake()
    {
        tileMapManager = FindObjectOfType<TileMapManager>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 유닛이 해당 타일에 들어왔을 때 처리 로직
        if (collision.GetComponent<UnitFSM>() == null)
            return;

        tileMapManager.SetTileStatus(Position, -1); // 점유됨으로 설정
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.GetComponent<UnitFSM>() == null)
            return;

        tileMapManager.SetTileStatus(Position, 0);
    }
}
