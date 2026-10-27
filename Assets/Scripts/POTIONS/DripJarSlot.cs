using UnityEngine;

public class DripJarSlot : MonoBehaviour
{
    [SerializeField] private Transform lockPoint;
    [SerializeField, Min(0.01f)] private float removalRearmDistance = 0.5f;

    [Header("SFX")]
    [SerializeField] private AudioClip jarPlacedSfx;
    [SerializeField] private AudioClip jarRemovedSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

    private IngredientJar lockedJar;
    private IngredientJar recentlyRemovedJar;
    private int changeVersion;

    public IngredientJar LockedJar => lockedJar;
    public bool HasLockedJar => lockedJar != null;
    public bool IsRemovalLocked { get; private set; }
    public int ChangeVersion => changeVersion;

    private void Update()
    {
        if (recentlyRemovedJar == null || lockPoint == null)
            return;

        // Re-arm only once the jar has truly been carried away. Re-parenting
        // during pickup can produce misleading trigger exit/enter events.
        if (Vector3.Distance(
                recentlyRemovedJar.transform.position,
                lockPoint.position
            ) >= removalRearmDistance)
        {
            recentlyRemovedJar = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (HasLockedJar)
            return;
        
        IngredientJar jar = other.GetComponentInParent<IngredientJar>();

        if (jar == null || !jar.CanLockInSlot) {
            return;
        }

        if (jar == recentlyRemovedJar)
            return;

        lockedJar = jar;
        changeVersion++;
        jar.LockToSlot(this, lockPoint);
        SfxAudio.PlayAt(jarPlacedSfx, lockPoint != null ? lockPoint.position : transform.position, sfxVolume);
    }

    public bool TryRemoveJar()
    {
        if (lockedJar == null || IsRemovalLocked)
            return false;

        IngredientJar jarToRemove = lockedJar;
        lockedJar = null;
        changeVersion++;
        recentlyRemovedJar = jarToRemove;
        jarToRemove.UnlockFromSlot();
        SfxAudio.PlayAt(jarRemovedSfx, transform.position, sfxVolume);
        return true;
    }

    /// <summary>Used by irreversible station actions to keep the inserted jar in place.</summary>
    public void SetRemovalLocked(bool locked)
    {
        IsRemovalLocked = locked;
    }
}
