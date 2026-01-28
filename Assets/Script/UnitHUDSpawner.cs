using UnityEngine;

public class UnitHUDSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private UnitHUD hudPrefab;   // UnitBar_World.prefab

    [Header("Attach")]
    [SerializeField] private Transform anchor;    // 바 붙일 위치(없으면 자기 자신)
    [SerializeField] private Vector3 localOffset = new Vector3(0, -1.8f, 0);

    private UnitHUD hudInstance;
    private Unit unit;
    
    public Transform HudTransform => hudInstance != null ? hudInstance.transform : null;

    void Awake()
    {
        unit = GetComponent<Unit>();
        if (anchor == null) anchor = transform;
    }

    void OnEnable()
    {
        if (hudInstance == null)
        {
            hudInstance = Instantiate(hudPrefab, anchor);
            hudInstance.transform.localPosition = localOffset;
            hudInstance.transform.localRotation = Quaternion.identity;
            hudInstance.transform.localScale = Vector3.one;
        }

        hudInstance.Bind(unit);
        hudInstance.gameObject.SetActive(true);
    }

    void OnDestroy()
    {
        if (hudInstance != null)
            Destroy(hudInstance.gameObject);
    }
}