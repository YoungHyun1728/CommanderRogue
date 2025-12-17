using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TileData
{
    public Vector2Int Position; // 타일 좌표
    public int Status; // 타일 상태 (0: 비어 있음, -1: 이동 불가)

    public TileData(Vector2Int position, int status)
    {
        Position = position;
        Status = status;
    }
}
