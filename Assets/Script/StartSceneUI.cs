using UnityEngine;
using UnityEngine.SceneManagement;

public class StartSceneUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private LoadingOverlay loadingOverlay;
    
    [Header("Audio")]
    [SerializeField] private BgmId titleBgm = BgmId.Title;
    [SerializeField] private float titleBgmFade = 0.6f;

    private void Start()
    {
        // 타이틀 진입 시 BGM 재생
        AudioManager.Instance?.PlayBgm(titleBgm, titleBgmFade);
    }

    public void OnClickNewGame()
    {
        SaveManager.instance?.CancelLoadRequest();
        SaveManager.pendingAutoLoad = false;
        Time.timeScale = 1f;             // 혹시 정지돼 있었으면 복구
        GameManager.instance?.OnRunFinished(default); // 필요 시 기록 초기화

        // 로딩 연출이 세팅돼 있으면 사용, 없으면 기존 방식으로 폴백
        if (loadingOverlay != null)
        {
            loadingOverlay.BeginLoad(gameSceneName);
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    public void OnClickContinue()
    {
        Time.timeScale = 1f;
        if (SaveManager.instance != null)
        {
            SaveManager.instance.loadRequested = true;
            SaveManager.instance.LoadGame();

            if (!SaveManager.instance.HasPendingRunLoad)
            {
                ToastManager.Instance?.Show("저장된 게임이 없습니다.", 0.4f, 0.2f);
                return;
            }
        }
        else
        {
            // SaveManager가 타이틀 씬에 없으면 다음 씬에서 자동 로드 플래그를 사용
            SaveManager.pendingAutoLoad = true;
        }

        if (loadingOverlay != null)
        {
            loadingOverlay.BeginLoad(gameSceneName);
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
