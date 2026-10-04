using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoopingSoundPlayer : MonoBehaviour
{
    [Header("Sounds")]
    [SerializeField] private List<AudioClip> clips = new();

    [Header("Mode")]
    [Tooltip("TRUE = all clips loop together. FALSE = random clips play one after another.")]
    [SerializeField] private bool layered = true;

    [Header("Audio")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 1f;

    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;

    [Tooltip("TRUE = Linear rolloff. FALSE = Logarithmic rolloff.")]
    [SerializeField] private bool useLinearRolloff = false;
    

    private readonly List<AudioSource> sources = new();

    private void Start()
    {
        if (clips == null || clips.Count == 0)
            return;

        if (layered)
        {
            StartLayered();
        }
        else
        {
            StartCoroutine(RandomLoop());
        }
    }

    private void StartLayered()
    {
        foreach (AudioClip clip in clips)
        {
            if (clip == null)
                continue;

            AudioSource source = gameObject.AddComponent<AudioSource>();

            ConfigureSource(source);

            source.clip = clip;
            source.loop = true;
            source.outputAudioMixerGroup = AudioManager.Instance != null
                ? AudioManager.Instance.SfxMixerGroup
                : null;
            source.Play();

            sources.Add(source);
        }
    }

    private IEnumerator RandomLoop()
    {
        while (true)
        {
            AudioClip clip = clips[Random.Range(0, clips.Count)];

            if (clip == null)
            {
                yield return null;
                continue;
            }

            AudioSource source = gameObject.AddComponent<AudioSource>();

            ConfigureSource(source);

            source.clip = clip;
            source.loop = false;
            source.outputAudioMixerGroup = AudioManager.Instance != null
                ? AudioManager.Instance.SfxMixerGroup
                : null;
            source.Play();

            sources.Add(source);

            yield return new WaitForSeconds(clip.length);

            sources.Remove(source);
            Destroy(source);
        }
    }

    private void ConfigureSource(AudioSource source)
    {
        source.volume = volume;
        source.spatialBlend = spatialBlend;

        source.minDistance = minDistance;
        source.maxDistance = maxDistance;

        source.rolloffMode = useLinearRolloff
            ? AudioRolloffMode.Linear
            : AudioRolloffMode.Logarithmic;

        source.playOnAwake = false;
    }
}
