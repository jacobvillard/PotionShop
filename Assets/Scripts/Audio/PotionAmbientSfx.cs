using UnityEngine;

/// <summary>Optional looping potion ambience. Add to any potion prefab.</summary>
public class PotionAmbientSfx : MonoBehaviour
{
    [SerializeField] private AudioClip loopingSfx;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField, Min(0f)] private float minDistance = 1f;
    [SerializeField, Min(0f)] private float maxDistance = 8f;

    private AudioSource source;

    private void Awake()
    {
        source = SfxAudio.CreateLoopSource(
            gameObject, loopingSfx, volume, 1f, minDistance, maxDistance);
    }

    private void OnEnable()
    {
        if (source != null && !source.isPlaying)
        {
            SfxAudio.RouteToSfxMixer(source);
            source.Play();
        }
    }

    private void OnDisable()
    {
        if (source != null)
            source.Stop();
    }
}
