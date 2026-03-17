using UnityEngine;

public interface IAudioService
{
    void PlayBgm(BgmId id, float fadeSeconds = 0.5f, bool loop = true);
    void StopBgm(float fadeSeconds = 0.25f);

    void PlaySfx(SfxId id, Vector3? worldPos = null, float volumeScale = 1f);
    void PlayVoice(VoiceId id, Vector3? worldPos = null, float volumeScale = 1f);

    void SetVolumeMaster(float value01);
    void SetVolumeBgm(float value01);
    void SetVolumeSfx(float value01);

    float GetVolumeMaster();
    float GetVolumeBgm();
    float GetVolumeSfx();
}
