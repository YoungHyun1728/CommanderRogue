using UnityEngine;

public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [SerializeField] private ToastMessage toastPrefab;
    [SerializeField] private Transform toastRoot;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Show(string msg, float duration = 1.2f, float fadeTime = 0.8f)
    {
        if (toastPrefab == null || toastRoot == null) return;

        var toast = Instantiate(toastPrefab, toastRoot);
        toast.Play(msg, duration, fadeTime);
    }
}
