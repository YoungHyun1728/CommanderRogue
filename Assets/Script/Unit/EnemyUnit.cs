using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyUnit : Unit
{
    private TileMapManager tileMapManager;

    //가장 가까운 적을 설정해 주는 함수
    private GameObject FindClosestEnemy()
    {
        GameObject closestEnemy = null;
        float closestDistance = float.MaxValue;

        foreach (GameObject enemy in tileMapManager.playerUnits)
        {
            if (enemy == null) continue;

            Vector2Int enemyTilePosition = tileMapManager.GetTileFromWorldPosition(enemy.transform.position);
            float distance = Vector2Int.Distance(currentTilePosition, enemyTilePosition);

            if (distance < closestDistance)
            {
                closestEnemy = enemy;
                closestDistance = distance;
            }
        }

        return closestEnemy;
    }
}
