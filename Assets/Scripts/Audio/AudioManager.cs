using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup ambMixerGroup;

    public AudioMixerGroup SfxMixerGroup => sfxMixerGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetSfxMixerVolume(SettingsManager.Current != null
            ? SettingsManager.Current.sfxVolume
            : 1f);
    }
    
    public void SetAmbienceMixerVolume(float volume)
    {
        if (ambMixerGroup == null)
            return;
        
        float dB = volume <= 0.0001f
            ? -80f
            : Mathf.Log10(volume) * 20f;

        ambMixerGroup.audioMixer.SetFloat("AmbienceVolume", dB);
    }

    public void SetSfxMixerVolume(float linearVolume)
    {
        if (sfxMixerGroup == null)
            return;

        float decibels = linearVolume > 0.0001f
            ? Mathf.Log10(linearVolume) * 20f
            : -80f;
        sfxMixerGroup.audioMixer.SetFloat("SFXVolume", decibels);
    }

    public void PlayOneShotAtLocation(
        AudioClip clip,
        Vector3 position,
        float volume = 1f,
        float spatialBlend = 1f)
    {
        if (clip == null)
            return;

        GameObject audioObject =
            new GameObject("OneShotAudio_" + clip.name);

        audioObject.transform.position = position;

        AudioSource source =
            audioObject.AddComponent<AudioSource>();

        source.clip = clip;
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = spatialBlend;

        source.Play();

        Destroy(audioObject, clip.length + 0.1f);
    }
    
    public void PlayOneShot(
        AudioClip clip,
        float volume = 1f,
        float spatialBlend = 0f)
    {
        if (clip == null)
            return;

        GameObject audioObject =
            new GameObject("OneShotAudio_" + clip.name);

        DontDestroyOnLoad(audioObject);

        AudioSource source =
            audioObject.AddComponent<AudioSource>();

        source.clip = clip;
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = spatialBlend;
        source.playOnAwake = false;

        source.Play();

        Destroy(audioObject, clip.length + 0.1f);
    }
}
