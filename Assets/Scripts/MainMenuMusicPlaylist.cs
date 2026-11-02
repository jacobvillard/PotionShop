using System.Collections;
using UnityEngine;

/// <summary>Plays the main-menu music in its assigned order and loops the full playlist.</summary>
public class MainMenuMusicPlaylist : MonoBehaviour
{
    [SerializeField] private AudioClip[] playlist;
    [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
    [Tooltip("Applied after the in-game music setting; 50 makes a 1% setting play at 50% on the title screen.")]
    [SerializeField, Range(0f, 100f)] private float menuVolumeBoost = 50f;
    [SerializeField, Min(0f)] private float startDelay = 0.6f;

    private AudioSource[] sources;
    private Coroutine playlistRoutine;

    private void OnEnable()
    {
        CreateSources();
        playlistRoutine = StartCoroutine(PlayPlaylist());
    }

    private void Update()
    {
        if (sources == null)
            return;

        float configuredVolume = SettingsManager.Current != null
            ? SettingsManager.Current.musicVolume
            : volume;
        float currentVolume = Mathf.Clamp01(configuredVolume * menuVolumeBoost);

        foreach (AudioSource source in sources)
            source.volume = currentVolume;
    }

    private void OnDisable()
    {
        if (playlistRoutine != null)
            StopCoroutine(playlistRoutine);

        if (sources == null)
            return;
        foreach (AudioSource source in sources)
            source.Stop();
    }

    private void CreateSources()
    {
        if (sources != null)
            return;

        sources = new[] { CreateSource("Main Menu Music A"), CreateSource("Main Menu Music B") };
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        return source;
    }

    private IEnumerator PlayPlaylist()
    {
        CreateSources();
        if (sources == null || sources.Length == 0)
            yield break;

        if (playlist == null || playlist.Length == 0)
            yield break;

        if (startDelay > 0f)
            yield return new WaitForSecondsRealtime(startDelay);

        int trackIndex = 0;
        int sourceIndex = 0;
        while (true)
        {
            AudioClip track = playlist[trackIndex];
            if (track == null)
            {
                trackIndex = (trackIndex + 1) % playlist.Length;
                continue;
            }

            sourceIndex = Mathf.Clamp(sourceIndex, 0, sources.Length - 1);
            AudioSource source = sources[sourceIndex];
            if (source == null)
            {
                CreateSources();
                if (sources == null || sources.Length == 0)
                    yield break;

                sourceIndex %= sources.Length;
                source = sources[sourceIndex];
                if (source == null)
                    yield break;
            }
            source.clip = track;
            source.Play();
            yield return new WaitForSecondsRealtime(track.length);

            trackIndex = (trackIndex + 1) % playlist.Length;
            sourceIndex = (sourceIndex + 1) % sources.Length;
        }
    }
}
