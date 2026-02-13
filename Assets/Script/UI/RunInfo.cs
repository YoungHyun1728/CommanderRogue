using TMPro;
using UnityEngine;

public class RunInfo : MonoBehaviour
{
    [SerializeField] TMP_Text tmpbiome;
    [SerializeField] TMP_Text tmpround;
    [SerializeField] TMP_Text tmpgold;
    [SerializeField] RunManager run;

    int _round;
    int _gold;
    string _biome;

    void Update()
    {
        int round = run.currentLevel;
        int gold = run.gold;
        string biome = BiomeText.ToDisplayName(run.CurrentBiome);

        // 변경 없으면 UI 갱신 스킵
        if (round == _round && gold == _gold && biome == _biome )
            return;
        

        _round = round;
        _gold = gold;
        _biome = biome;

        tmpbiome.text = $"{_biome}";
        tmpround.text = $"{_round}";
        tmpgold.text = $"{_gold}";

        if(round == 0)
            tmpround.text = $"Start";

    }
}
