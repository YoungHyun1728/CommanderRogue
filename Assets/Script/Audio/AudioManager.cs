using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour, IAudioService
{
    public static IAudioService Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private BgmTable bgmTable;
    [SerializeField] private SfxTable sfxTable;
    [SerializeField] private VoiceTable voiceTable;

    [Header("Mixer (Optional)")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string masterParam = "MasterVolume";
    [SerializeField] private string bgmParam = "BgmVolume";
    [SerializeField] private string sfxParam = "SfxVolume";

    [Header("SFX Pool")]
    [SerializeField] private int sfxPoolSize = 16;
    [SerializeField] private Transform sfxRoot;

    private AudioSource[] sfxPool;
    private int sfxIndex;

    private AudioSource bgmA;
    private AudioSource bgmB;
    private AudioSource currentBgmSource;
    private bool bgmToggle;
    private Coroutine bgmFadeRoutine;
    private BgmEntry currentBgm;

    private const string KeyMaster = "audio.master";
    private const string KeyBgm = "audio.bgm";
    private const string KeySfx = "audio.sfx";
    private float masterVolume = 1f;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolumes();
        BuildBgmSources();
        BuildSfxPool();
        ApplyMixerVolumes();
        RefreshBgmVolume();
    }

    private void BuildBgmSources()
    {
        bgmA = gameObject.AddComponent<AudioSource>();
        bgmB = gameObject.AddComponent<AudioSource>();
        bgmA.playOnAwake = bgmB.playOnAwake = false;
        bgmA.loop = bgmB.loop = true;
    }

    private void BuildSfxPool()
    {
        if (sfxRoot == null)
        {
            sfxRoot = new GameObject("SFX_Pool").transform;
            sfxRoot.SetParent(transform);
        }

        sfxPool = new AudioSource[sfxPoolSize];
        for (int i = 0; i < sfxPoolSize; i++)
        {
            var go = new GameObject($"SFX_{i}");
            go.transform.SetParent(sfxRoot);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            sfxPool[i] = src;
        }
    }

    #region Volume

    private static float ToDecibel(float value)
    {
        if (value <= 0.0001f) return -80f;
        return Mathf.Log10(value) * 20f;
    }

    private void LoadVolumes()
    {
        masterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        bgmVolume = PlayerPrefs.GetFloat(KeyBgm, 1f);
        sfxVolume = PlayerPrefs.GetFloat(KeySfx, 1f);
    }

    private void SaveVolumes()
    {
        PlayerPrefs.SetFloat(KeyMaster, masterVolume);
        PlayerPrefs.SetFloat(KeyBgm, bgmVolume);
        PlayerPrefs.SetFloat(KeySfx, sfxVolume);
    }

    private void ApplyMixerVolumes()
    {
        if (mixer == null) return;

        mixer.SetFloat(masterParam, ToDecibel(masterVolume));
        mixer.SetFloat(bgmParam, ToDecibel(bgmVolume));
        mixer.SetFloat(sfxParam, ToDecibel(sfxVolume));
    }

    private void RefreshBgmVolume()
    {
        float target = masterVolume * bgmVolume;
        if (currentBgm != null)
        {
            target *= currentBgm.volume;
        }

        if (bgmA != null && bgmA.isPlaying) bgmA.volume = target;
        if (bgmB != null && bgmB.isPlaying) bgmB.volume = target;
    }

    public void SetVolumeMaster(float value01)
    {
        masterVolume = Mathf.Clamp01(value01);
        SaveVolumes();
        ApplyMixerVolumes();
        RefreshBgmVolume();
    }

    public void SetVolumeBgm(float value01)
    {
        bgmVolume = Mathf.Clamp01(value01);
        SaveVolumes();
        ApplyMixerVolumes();
        RefreshBgmVolume();
    }

    public void SetVolumeSfx(float value01)
    {
        sfxVolume = Mathf.Clamp01(value01);
        SaveVolumes();
        ApplyMixerVolumes();
    }

    public float GetVolumeMaster() => masterVolume;
    public float GetVolumeBgm() => bgmVolume;
    public float GetVolumeSfx() => sfxVolume;

    #endregion

    #region BGM

    public void PlayBgm(BgmId id, float fadeSeconds = 0.5f, bool loop = true)
    {
        var entry = bgmTable != null ? bgmTable.Get(id) : null;
        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"[AudioManager] BGM not found: {id}");
            return;
        }

        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
        }

        currentBgm = entry;
        bgmFadeRoutine = StartCoroutine(FadeBgm(entry, fadeSeconds, loop));
    }

    public void StopBgm(float fadeSeconds = 0.25f)
    {
        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
        }

        var current = currentBgmSource != null ? currentBgmSource : (bgmToggle ? bgmA : bgmB);
        if (current != null && current.isPlaying)
        {
            bgmFadeRoutine = StartCoroutine(FadeOut(current, fadeSeconds));
        }

        currentBgm = null;
        currentBgmSource = null;
    }

    private IEnumerator FadeBgm(BgmEntry next, float fadeSeconds, bool loop)
    {
        var from = bgmToggle ? bgmB : bgmA;
        var to = bgmToggle ? bgmA : bgmB;
        bgmToggle = !bgmToggle;

        to.clip = next.clip;
        to.loop = loop && next.loop;
        to.volume = 0f;
        to.Play();

        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeSeconds > 0f ? elapsed / fadeSeconds : 1f;
            float targetVol = masterVolume * bgmVolume * next.volume;
            to.volume = Mathf.Lerp(0f, targetVol, t);
            from.volume = Mathf.Lerp(from.volume, 0f, t);
            yield return null;
        }

        from.Stop();
        from.volume = 0f;
        to.volume = masterVolume * bgmVolume * next.volume;
        currentBgmSource = to;
    }

    private IEnumerator FadeOut(AudioSource source, float fadeSeconds)
    {
        float start = source.volume;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeSeconds > 0f ? elapsed / fadeSeconds : 1f;
            source.volume = Mathf.Lerp(start, 0f, t);
            yield return null;
        }

        source.Stop();
        source.volume = 0f;

        if (currentBgmSource == source)
        {
            currentBgmSource = null;
        }
    }

    #endregion

    #region SFX / Voice

    private AudioSource NextSfxSource()
    {
        sfxIndex = (sfxIndex + 1) % sfxPool.Length;
        return sfxPool[sfxIndex];
    }

    public void PlaySfx(SfxId id, Vector3? worldPos = null, float volumeScale = 1f)
    {
        if (sfxTable == null)
        {
            Debug.LogWarning("[AudioManager] SfxTable is missing.");
            return;
        }

        var entry = sfxTable.Get(id);
        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"[AudioManager] SFX not found: {id}");
            return;
        }

        if (entry.cooldown > 0f && Time.unscaledTime - entry.lastPlayTime < entry.cooldown)
        {
            return;
        }

        entry.lastPlayTime = Time.unscaledTime;

        var src = NextSfxSource();
        src.transform.position = worldPos ?? transform.position;
        src.spatialBlend = worldPos.HasValue ? 1f : 0f;
        src.pitch = Random.Range(entry.pitchMin, entry.pitchMax);
        float vol = masterVolume * sfxVolume * entry.volume * volumeScale;
        src.PlayOneShot(entry.clip, vol);
    }

    public void PlayVoice(VoiceId id, Vector3? worldPos = null, float volumeScale = 1f)
    {
        if (voiceTable == null)
        {
            Debug.LogWarning("[AudioManager] VoiceTable is missing.");
            return;
        }

        var entry = voiceTable.Get(id);
        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"[AudioManager] Voice not found: {id}");
            return;
        }

        var src = NextSfxSource();
        src.transform.position = worldPos ?? transform.position;
        src.spatialBlend = worldPos.HasValue ? 1f : 0f;
        src.pitch = 1f;
        float vol = masterVolume * sfxVolume * entry.volume * volumeScale;
        src.PlayOneShot(entry.clip, vol);
    }

    #endregion
}
