using System.Collections;
using TMPro;
using UnityEngine;

public class ToastMessage : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private CanvasGroup canvasGroup;

    public void Play(string msg, float duration, float fadeTime)
    {
        if (text != null) text.text = msg;
        StopAllCoroutines();
        StartCoroutine(CoPlay(duration, fadeTime));
    }

    private IEnumerator CoPlay(float duration, float fadeTime)
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(duration);

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            yield return null;
        }

        Destroy(gameObject);
    }


}