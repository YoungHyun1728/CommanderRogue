using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 현재 바이옴에 따라 배경을 변경하는 스크립트
public class BGContorller : MonoBehaviour
{
    [System.Serializable]
    public class BiomeSprite
    {
        public BiomeType biome;
        public Sprite sprite;
    }

    [SerializeField] private RunManager runManager;
    [SerializeField] private SpriteRenderer bgRenderer;
    [SerializeField] private List<BiomeSprite> biomeSprites = new();

    private Dictionary<BiomeType, Sprite> _map;

    private void Awake()
    {
        if (runManager == null) runManager = FindAnyObjectByType<RunManager>();
        if (bgRenderer == null) bgRenderer = GetComponent<SpriteRenderer>();

        _map = new Dictionary<BiomeType, Sprite>();
        foreach (var bs in biomeSprites)
        {
            if (bs == null) continue;
            _map[bs.biome] = bs.sprite;
        }
    }

    private void OnEnable()
    {
        if (runManager != null)
            runManager.OnBiomeChanged += HandleBiomeChanged;
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.OnBiomeChanged -= HandleBiomeChanged;
    }

    private void Start()
    {
        if (runManager != null)
            HandleBiomeChanged(runManager.CurrentBiome);
    }

    private void HandleBiomeChanged(BiomeType biome)
    {
        if (bgRenderer == null) return;

        if (_map.TryGetValue(biome, out var sprite) && sprite != null)
            bgRenderer.sprite = sprite;
    }

}
