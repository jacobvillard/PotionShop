using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonAudio : MonoBehaviour,
    IPointerEnterHandler,
    IPointerClickHandler
{
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Range(0f, 1f)]
    [SerializeField] private float hoverVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 1f;

    private AudioSource fallbackSource;

    public void Configure(AudioClip hoverClip, AudioClip clickClip, float configuredHoverVolume, float configuredClickVolume)
    {
        hoverSound = hoverClip;
        clickSound = clickClip;
        hoverVolume = Mathf.Clamp01(configuredHoverVolume);
        clickVolume = Mathf.Clamp01(configuredClickVolume);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Play(hoverSound, hoverVolume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Play(clickSound, clickVolume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOneShot(clip, volume);
            return;
        }

        if (fallbackSource == null)
        {
            fallbackSource = gameObject.AddComponent<AudioSource>();
            fallbackSource.playOnAwake = false;
            fallbackSource.spatialBlend = 0f;
        }

        fallbackSource.PlayOneShot(clip, volume);
    }
}
