using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;    // instance를 static으로 선언해 전역에서 사용가능
    
    public double lastRunScore;
    public GameResultData lastRunResult;

    private void EnsureMetaProgress()
    {
        // MetaProgressManager는 싱글톤으로 lazy 생성되므로 여기서 한 번 접근만 해도 된다.
        var _ = MetaProgressManager.Instance;
    }

    void Awake()
    {
        if(instance != null)
        {
            Destroy(instance);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // 씬 이동 후에도 파괴되지 않음
        EnsureMetaProgress();
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void OnRunFinished(GameResultData result)
    {
        lastRunResult = result;
        lastRunScore = result.Score;
        MetaProgressManager.Instance?.AddScore(result.Score);
        Debug.Log($"[GameManager] Run finished. Score={lastRunScore:F0}, Round={result.Round}, Clear={result.IsClear}");
    }
}
