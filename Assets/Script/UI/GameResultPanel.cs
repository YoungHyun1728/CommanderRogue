using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 오버 / 클리어 패널 UI를 묶어두는 단순 뷰.
/// </summary>
public class GameResultPanel : MonoBehaviour
{
    [Header("Root & Title")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;

    [Header("공통 정보")]
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text killText;
    [SerializeField] private TMP_Text powerText;
    [SerializeField] private TMP_Text scoreText;

    [Header("최고 전투력 유닛")]
    [SerializeField] private GameObject topUnitGroup;
    [SerializeField] private TMP_Text topUnitNameText;
    [SerializeField] private Image topUnitPortraitImage;

    [Header("Challenge Mode (Clear 전용)")]
    [SerializeField] private GameObject challengeGroup;
    [SerializeField] private TMP_InputField challengeNameInput;
    [SerializeField] private Button challengeButton;
    [SerializeField] private Button challengeOpenButton;

    [Header("버튼")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;

    [Header("Scene Names (옵션)")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string titleSceneName = "StartScene";

    [SerializeField] private bool pauseOnOpen = true;

    private float _cachedTimeScale = 1f;
    private Action _onRetry;
    private Action _onTitle;
    private Action<string> _onChallenge;
    private GameResultData _cachedData;

    void Awake()
    {
        if (root == null) root = gameObject;
        if (retryButton != null) retryButton.onClick.AddListener(HandleRetry);
        if (titleButton != null) titleButton.onClick.AddListener(HandleTitle);
        if (challengeButton != null) challengeButton.onClick.AddListener(HandleChallenge);
        if (challengeOpenButton != null) challengeOpenButton.onClick.AddListener(HandleOpenChallenge);
        HideImmediate();
    }

    void OnDestroy()
    {
        if (retryButton != null) retryButton.onClick.RemoveListener(HandleRetry);
        if (titleButton != null) titleButton.onClick.RemoveListener(HandleTitle);
        if (challengeButton != null) challengeButton.onClick.RemoveListener(HandleChallenge);
        if (challengeOpenButton != null) challengeOpenButton.onClick.RemoveListener(HandleOpenChallenge);
    }

    public void Configure(Action onRetry, Action onTitle, Action<string> onChallenge = null)
    {
        _onRetry = onRetry;
        _onTitle = onTitle;
        _onChallenge = onChallenge;
    }

    public void Show(GameResultData data)
    {
        _cachedData = data;
        ApplyTexts(data);

        /*if (pauseOnOpen)
        {
            _cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }*/

        bool canChallenge = data.IsClear && _onChallenge != null;
        if (challengeGroup != null) challengeGroup.SetActive(false); // 기본 닫힘
        if (challengeOpenButton != null) challengeOpenButton.gameObject.SetActive(canChallenge);
        if (challengeButton != null) challengeButton.gameObject.SetActive(true);
        if (retryButton != null) retryButton.gameObject.SetActive(true);

        if (canChallenge && challengeNameInput != null && string.IsNullOrWhiteSpace(challengeNameInput.text))
        {
            challengeNameInput.text = GetDefaultPartyName(data);
        }

        root.SetActive(true);
    }

    private void ApplyTexts(GameResultData data)
    {
        if (titleText != null) titleText.text = data.IsClear ? "Game Clear" : "Game Over";
        if (roundText != null) roundText.text = data.Round.ToString();
        if (goldText != null) goldText.text = data.Gold.ToString();
        if (killText != null) killText.text = data.EnemyKills.ToString();
        if (powerText != null) powerText.text = Mathf.RoundToInt((float)data.PartyPower).ToString();
        if (scoreText != null) scoreText.text = Mathf.RoundToInt((float)data.Score).ToString();

        bool hasTopUnit = !string.IsNullOrWhiteSpace(data.TopUnitName) || data.TopUnitPortrait != null;
        if (topUnitGroup != null) topUnitGroup.SetActive(hasTopUnit);

        if (topUnitNameText != null)
            topUnitNameText.text = hasTopUnit ? (data.TopUnitName ?? "-") : "-";

        if (topUnitPortraitImage != null)
        {
            topUnitPortraitImage.sprite = data.TopUnitPortrait;
            topUnitPortraitImage.enabled = data.TopUnitPortrait != null;
        }
    }

    public void HideImmediate()
    {
        if (pauseOnOpen)
            Time.timeScale = _cachedTimeScale;

        if (root != null)
            root.SetActive(false);
    }

    private void HandleRetry()
    {
        HideImmediate();

        if (_onRetry != null)
        {
            _onRetry.Invoke();
            return;
        }

        if (!string.IsNullOrEmpty(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
    }

    private void HandleTitle()
    {
        HideImmediate();

        if (_onTitle != null)
        {
            _onTitle.Invoke();
            return;
        }

        if (!string.IsNullOrEmpty(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
    }

    private void HandleChallenge()
    {
        if (_onChallenge == null) return;

        string name = challengeNameInput != null ? challengeNameInput.text : null;
        if (string.IsNullOrWhiteSpace(name))
            name = GetDefaultPartyName(_cachedData);

        _onChallenge.Invoke(name);
    }

    private void HandleOpenChallenge()
    {
        if (challengeGroup == null) return;
        bool next = !challengeGroup.activeSelf;
        challengeGroup.SetActive(next);

        if (next && challengeNameInput != null && string.IsNullOrWhiteSpace(challengeNameInput.text))
            challengeNameInput.text = GetDefaultPartyName(_cachedData);
    }

    private string GetDefaultPartyName(GameResultData data)
    {
        int round = Mathf.Max(1, data.Round);
        return $"Party-R{round:000}";
    }
}
