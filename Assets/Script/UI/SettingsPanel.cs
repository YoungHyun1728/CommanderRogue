using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button goTitleButton;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (goTitleButton != null)
            goTitleButton.onClick.AddListener(GoToTitle);

        SyncFromAudio();
    }

    private void OnEnable()
    {
        SyncFromAudio();
    }

    public void Open()
    {
        SyncFromAudio();
        if (panelRoot != null) panelRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnBgmChanged(float value)
    {
        AudioManager.Instance?.SetVolumeBgm(value);
    }

    public void OnSfxChanged(float value)
    {
        AudioManager.Instance?.SetVolumeSfx(value);
    }

    private void SyncFromAudio()
    {
        var audio = AudioManager.Instance as AudioManager;
        if (audio == null) return;

        float bgm = audio.GetVolumeBgm();
        float sfx = audio.GetVolumeSfx();

        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(bgm);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfx);
    }

    private void GoToTitle()
    {
        Close();
        RunManager.Instance?.SaveAndReturnToTitle();
    }
}
