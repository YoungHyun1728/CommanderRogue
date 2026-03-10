using UnityEngine;
using UnityEngine.SceneManagement;

public class StartSceneUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";

    public void OnClickNewGame()
    {
        Time.timeScale = 1f;             // 혹시 정지돼 있었으면 복구
        GameManager.instance?.OnRunFinished(default); // 필요 시 기록 초기화
        SceneManager.LoadScene(gameSceneName);
    }
}