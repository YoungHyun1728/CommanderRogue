using TMPro;
using UnityEngine;

public class RunInfo : MonoBehaviour
{
    [SerializeField] TMP_Text tmpbiome;
    [SerializeField] TMP_Text tmpround;
    [SerializeField] TMP_Text tmpgold;
    [SerializeField] GameObject challengeNamesContainer; // 부모에 "우리파티", "VS", "상대파티" 텍스트가 자식으로 붙어 있음
    [SerializeField] TMP_Text tmpChallengeName;
    [SerializeField] TMP_Text tmpOpponentChallengeName;
    [SerializeField] RunManager run;

    int _round;
    int _gold;
    string _biome;
    string _challengeName;
    string _opponentName;

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

        bool showNames = run.ChallengeModeActive;
        if (challengeNamesContainer != null)
            challengeNamesContainer.SetActive(showNames);
        else
        {
            // 부모가 없을 경우 개별 텍스트를 토글
            if (tmpChallengeName != null) tmpChallengeName.gameObject.SetActive(showNames);
            if (tmpOpponentChallengeName != null) tmpOpponentChallengeName.gameObject.SetActive(showNames);
        }

        if (showNames && tmpChallengeName != null)
        {
            if (_challengeName != run.ChallengePartyName)
            {
                _challengeName = run.ChallengePartyName;
                tmpChallengeName.text = _challengeName;
            }
        }
        else
        {
            _challengeName = null;
        }

        if (showNames && tmpOpponentChallengeName != null)
        {
            if (_opponentName != run.ChallengeOpponentName)
            {
                _opponentName = run.ChallengeOpponentName;
                tmpOpponentChallengeName.text = _opponentName;
            }
        }
        else
        {
            _opponentName = null;
        }

    }
}
