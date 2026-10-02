using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = spatialBlend;

        source.Play();

        Destroy(audioObject, clip.length + 0.1f);
    }
}