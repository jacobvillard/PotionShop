using UnityEngine;

/// <summary>Optional per-prefab pickup and drop overrides for physical objects.</summary>
public class ObjectSfx : MonoBehaviour
{
    [SerializeField] private AudioClip pickupSfx;
    [SerializeField] private AudioClip dropSfx;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float dropVolume = 1f;

    public void PlayPickup() => SfxAudio.PlayAt(pickupSfx, transform.position, pickupVolume);
    public void PlayDrop() => SfxAudio.PlayAt(dropSfx, transform.position, dropVolume);
}
