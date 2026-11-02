using System.Collections;
using UnityEngine;

/// <summary>Plays the title-screen arrival sound even when MainMenu is run on its own.</summary>
public class MainMenuStartupSound : MonoBehaviour
{
    [SerializeField] private AudioClip startupSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField, Min(0f)] private float delay = 0.15f;

    private IEnumerator Start()
    {
        if (startupSound == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOneShot(startupSound, volume);
            yield break;
        }

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.PlayOneShot(startupSound, volume);
    }
}
