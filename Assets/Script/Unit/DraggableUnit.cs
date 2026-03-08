using UnityEngine;
using UnityEngine.EventSystems;

// Drag & drop player units in Ready state to move or swap positions
[RequireComponent(typeof(UnitFSM))]
[RequireComponent(typeof(Collider2D))]
public class DraggableUnit : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Preview Colors")]
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField] private Color swapColor  = new Color(0.1f, 0.6f, 1f, 0.4f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.35f);
    [Header("Preview Prefab")]
    [SerializeField] private GameObject previewPrefab;      // Prefab for drag preview (fallback to Quad if null)
    [SerializeField] private Camera dragCamera;             // Camera used for drag screen-to-world (battle cam)
    [SerializeField] private string previewLayerName = "PlayerUnit"; // Layer for preview object
    private RunManager run;
    private TileMapManager tileMap;
    private UnitFSM fsm;
    private UnitGridAgent agent;

    private GameObject preview;
    private SpriteRenderer previewRenderer;

    private Vector2Int startTile;
    private Vector3 startPos;
    private Vector2Int currentTile;
    private GameObject swapTarget;
    private bool isDragging;

    private void Awake()
    {
        run = FindAnyObjectByType<RunManager>();
        tileMap = FindAnyObjectByType<TileMapManager>();
        fsm = GetComponent<UnitFSM>();
        agent = GetComponent<UnitGridAgent>();
        EnsurePreview();
    }

    private void EnsurePreview()
    {
        if (preview != null) return;

        if (previewPrefab != null)
        {
            preview = Instantiate(previewPrefab);
            previewRenderer = preview.GetComponentInChildren<SpriteRenderer>();
        }

        if (previewRenderer != null)
            previewRenderer.sortingOrder = 5000;
        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer >= 0) preview.layer = layer;
        preview.SetActive(false);
    }

    private bool CanDrag()
    {
        return run != null &&
               run.currentRunState == RunState.Ready &&
               !run.isInBattle &&
               fsm != null &&
               tileMap != null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag()) return;
        isDragging = true;
        startTile = fsm.currentTilePosition;
        startPos = transform.position;
        preview.SetActive(true);
        UpdatePreview(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        UpdatePreview(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;
        preview.SetActive(false);

        if (!IsTileValid(currentTile, out bool willSwap, out GameObject target))
        {
            // Invalid drop: snap back
            transform.position = startPos;
            return;
        }

        // Update occupancy data
        tileMap.VacateTile(startTile, fsm.unitId);

        if (willSwap && target != null)
        {
            var otherFsm = target.GetComponent<UnitFSM>();
            var otherAgent = target.GetComponent<UnitGridAgent>();
            if (otherFsm != null)
            {
                var otherTile = otherFsm.currentTilePosition;
                tileMap.VacateTile(otherTile, otherFsm.unitId);

                // Swap occupancy
                tileMap.OccupyTile(otherTile, fsm.unitId);
                tileMap.OccupyTile(startTile, otherFsm.unitId);

                // Sync positions/tiles
                Vector3 thisPos = tileMap.GetTileCenterWorld(otherTile);
                Vector3 otherPos = tileMap.GetTileCenterWorld(startTile);
                transform.position = thisPos;
                if (agent != null) agent.ForceSyncToTile(otherTile);
                if (otherAgent != null) otherAgent.ForceSyncToTile(startTile);
                else if (otherFsm != null) otherFsm.currentTilePosition = startTile;
                target.transform.position = otherPos;
            }
        }
        else
        {
            tileMap.OccupyTile(currentTile, fsm.unitId);
            Vector3 pos = tileMap.GetTileCenterWorld(currentTile);
            transform.position = pos;
            if (agent != null) agent.ForceSyncToTile(currentTile);
            else fsm.currentTilePosition = currentTile;
        }
    }

    private void UpdatePreview(PointerEventData eventData)
    {
        Camera useCam = eventData.pressEventCamera ?? dragCamera ?? Camera.main;
        if (useCam == null) return;

        var world = useCam.ScreenToWorldPoint(eventData.position);
        world.z = 0f;
        currentTile = tileMap.GetTileFromWorldPosition(world);

        bool isValid = IsTileValid(currentTile, out bool willSwap, out GameObject target);
        preview.transform.position = tileMap.GetTileCenterWorld(currentTile);
        if (isValid)
            previewRenderer.color = willSwap ? swapColor : validColor;
        else
            previewRenderer.color = invalidColor;
    }

    private bool IsTileValid(Vector2Int tile, out bool willSwap, out GameObject targetUnit)
    {
        willSwap = false;
        targetUnit = null;

        // Tile must exist (walkable) regardless of current occupancy
        if (!tileMap.tilemap.HasTile(new Vector3Int(tile.x, tile.y, 0)))
            return false;

        // Limit to friendly side: x -6 ~ -1 only
        if (tile.x < -6 || tile.x > -1)
            return false;

        // Friendly unit present → allow swap
        targetUnit = tileMap.GetPlayerUnitAt(tile);
        if (targetUnit != null && targetUnit != gameObject)
        {
            willSwap = true;
            return true;
        }

        // Occupied by enemy/other → invalid
        if (tileMap.IsOccupied(tile))
            return false;

        // Empty tile OK
        return true;
    }
}

