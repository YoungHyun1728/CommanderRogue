using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingOverlay : MonoBehaviour
{
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private Animator runnerAnimator;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float dotInterval = 0.3f;
    [SerializeField] private bool persistAcrossScenes = true;

    private bool isLoading;
    private string baseLoadingText = "Loading";

    public void BeginLoad(string sceneName)
    {
        if (isLoading) return;
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

        if (loadingText != null) baseLoadingText = loadingText.text;
        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        isLoading = true;

        if (fade != null)
        {
            fade.blocksRaycasts = true; // 막 화면 동안 입력 차단
            yield return Fade(0f, 1f, fadeDuration);
        }

        if (runnerAnimator != null) runnerAnimator.SetTrigger("Run");
        var dotsRoutine = StartCoroutine(LoadingDots());

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) yield return null; // 0.9f ≈ 로드 완료
        yield return new WaitForSeconds(0.3f);         // 짧은 버퍼로 연출 여유

        op.allowSceneActivation = true;
        yield return null; // 새 씬 첫 프레임

        if (fade != null)
        {
            yield return Fade(1f, 0f, fadeDuration);
            fade.blocksRaycasts = false;
        }

        if (dotsRoutine != null) StopCoroutine(dotsRoutine);
        isLoading = false;
    }

    private IEnumerator LoadingDots()
    {
        if (loadingText == null) yield break;

        while (true)
        {
            for (int i = 0; i < 4; i++)
            {
                loadingText.text = baseLoadingText + new string('.', i);
                yield return new WaitForSeconds(dotInterval);
            }
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fade == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            fade.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        fade.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fade.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        fade.alpha = to;
    }
}
