using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmbientSoundManager : MonoBehaviour
{
    [System.Serializable]
    public class WeightedClip
    {
        public AudioClip clip;

        [Min(0.01f)]
        public float weight = 1f;
    }

    [System.Serializable]
    public class AmbientLayer
    {
        public string layerName;

        [Header("Sounds")]
        public List<WeightedClip> clips = new();

        [Header("Locations")]
        public List<Transform> playLocations = new();

        [Header("Timing")]
        [Tooltip("How long this layer keeps using the selected sound before choosing another.")]
        public float selectionDuration = 300f;

        [Tooltip("Minimum time between individual sound plays.")]
        public float minDelay = 5f;

        [Tooltip("Maximum time between individual sound plays.")]
        public float maxDelay = 15f;

        [Header("Audio")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(0f, 1f)]
        public float spatialBlend = 1f;

        public float minDistance = 3f;
        public float maxDistance = 30f;

        [Header("Behaviour")]
        [Tooltip("If enabled, the selected clip loops continuously for the selection duration.")]
        public bool continuousLoop;

        [Tooltip("If disabled, a new random position is selected for each sound.")]
        public bool keepSameLocation;
    }

    [SerializeField]
    private List<AmbientLayer> ambientLayers = new();

    private void Start()
    {
        foreach (AmbientLayer layer in ambientLayers)
        {
            StartCoroutine(RunLayer(layer));
        }
    }

    private IEnumerator RunLayer(AmbientLayer layer)
    {
        while (true)
        {
            WeightedClip selectedClip = GetWeightedClip(layer);

            if (selectedClip == null || selectedClip.clip == null)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (layer.continuousLoop)
            {
                yield return StartCoroutine(
                    PlayContinuousLoop(layer, selectedClip.clip)
                );
            }
            else
            {
                yield return StartCoroutine(
                    PlayOccasionalSounds(layer, selectedClip.clip)
                );
            }
        }
    }

    private IEnumerator PlayContinuousLoop(
        AmbientLayer layer,
        AudioClip clip)
    {
        List<GameObject> audioObjects = new();

        // If no locations are assigned, just play from manager position.
        if (layer.playLocations == null || layer.playLocations.Count == 0)
        {
            GameObject audioObject =
                new GameObject("Ambient_" + layer.layerName);

            audioObject.transform.position = transform.position;

            AudioSource source =
                audioObject.AddComponent<AudioSource>();

            ConfigureAudioSource(source, layer);

            source.clip = clip;
            source.loop = true;
            source.Play();

            audioObjects.Add(audioObject);
        }
        else
        {
            // Play the SAME loop at every assigned location.
            foreach (Transform location in layer.playLocations)
            {
                if (location == null)
                    continue;

                GameObject audioObject =
                    new GameObject(
                        $"Ambient_{layer.layerName}_{location.name}"
                    );

                audioObject.transform.position = location.position;

                AudioSource source =
                    audioObject.AddComponent<AudioSource>();

                ConfigureAudioSource(source, layer);

                source.clip = clip;
                source.loop = true;
                source.Play();

                audioObjects.Add(audioObject);
            }
        }

        yield return new WaitForSeconds(layer.selectionDuration);

        foreach (GameObject audioObject in audioObjects)
        {
            if (audioObject != null)
                Destroy(audioObject);
        }
    }

    private IEnumerator PlayOccasionalSounds(
        AmbientLayer layer,
        AudioClip selectedClip)
    {
        float timer = 0f;

        while (timer < layer.selectionDuration)
        {
            // Play at ALL assigned locations
            if (layer.playLocations != null &&
                layer.playLocations.Count > 0)
            {
                foreach (Transform location in layer.playLocations)
                {
                    if (location == null)
                        continue;

                    PlayOneShot(
                        selectedClip,
                        location.position,
                        layer
                    );
                }
            }
            else
            {
                // Fallback to manager location
                PlayOneShot(
                    selectedClip,
                    transform.position,
                    layer
                );
            }

            float delay = Random.Range(
                layer.minDelay,
                layer.maxDelay
            );

            timer += delay;

            yield return new WaitForSeconds(delay);
        }
    }

    private WeightedClip GetWeightedClip(AmbientLayer layer)
    {
        if (layer.clips == null || layer.clips.Count == 0)
            return null;

        float totalWeight = 0f;

        foreach (WeightedClip clip in layer.clips)
        {
            if (clip.clip != null)
                totalWeight += clip.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float randomValue = Random.Range(0f, totalWeight);

        float currentWeight = 0f;

        foreach (WeightedClip clip in layer.clips)
        {
            if (clip.clip == null)
                continue;

            currentWeight += clip.weight;

            if (randomValue <= currentWeight)
                return clip;
        }

        return layer.clips[0];
    }

    private Transform GetRandomLocation(AmbientLayer layer)
    {
        if (layer.playLocations == null ||
            layer.playLocations.Count == 0)
        {
            return null;
        }

        return layer.playLocations[
            Random.Range(0, layer.playLocations.Count)
        ];
    }

    private void PlayOneShot(
        AudioClip clip,
        Vector3 position,
        AmbientLayer layer)
    {
        GameObject audioObject =
            new GameObject("AmbientOneShot_" + clip.name);

        audioObject.transform.position = position;

        AudioSource source =
            audioObject.AddComponent<AudioSource>();

        ConfigureAudioSource(source, layer);

        source.clip = clip;
        source.loop = false;

        source.Play();

        Destroy(audioObject, clip.length + 0.1f);
    }

    private void ConfigureAudioSource(
        AudioSource source,
        AmbientLayer layer)
    {
        source.volume = layer.volume;
        source.spatialBlend = layer.spatialBlend;

        source.minDistance = layer.minDistance;
        source.maxDistance = layer.maxDistance;

        source.rolloffMode =
            AudioRolloffMode.Logarithmic;
    }
}