using UnityEngine;

public enum BiomeType
{
    Forest,     // 숲
    Plains,     // 평야
    DeepForest,
    Cave,
    Lake,
    Snow,
    Desert,
    Labyrinth   // 미궁 (마지막)
}

public static class BiomeText
{
    public static string ToDisplayName(BiomeType type)
    {
        return type switch
        {
            BiomeType.Forest     => "숲",
            BiomeType.Plains     => "평야",
            BiomeType.DeepForest => "깊은 숲",
            BiomeType.Cave       => "동굴",
            BiomeType.Lake       => "호수",
            BiomeType.Snow       => "설산",
            BiomeType.Desert     => "사막",
            BiomeType.Labyrinth  => "미궁",
            _ => type.ToString()
        };
    }
}