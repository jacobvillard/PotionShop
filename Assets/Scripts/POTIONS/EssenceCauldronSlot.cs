using UnityEngine;

/// <summary>
/// Snap point used by an essence extractor. It deliberately does not require
/// the cauldron to be empty: an extracted essence is an additional ingredient.
/// </summary>
public class EssenceCauldronSlot : MonoBehaviour
{
    [SerializeField] private Transform lockPoint;
    [SerializeField, Min(0.01f)] private float removalRearmDistance = 0.5f;

    private Cauldron lockedCauldron;
    private Cauldron recentlyRemovedCauldron;

    public Cauldron LockedCauldron => lockedCauldron;
    public bool HasLockedCauldron => lockedCauldron != null;

    private void Awake()
    {
        if (lockPoint == null)
            lockPoint = transform;
    }

    private void Update()
    {
        if (recentlyRemovedCauldron != null &&
            Vector3.Distance(recentlyRemovedCauldron.transform.position, lockPoint.position) >= removalRearmDistance)
            recentlyRemovedCauldron = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (HasLockedCauldron)
            return;

        Cauldron cauldron = other.GetComponentInParent<Cauldron>();
        if (cauldron == null || cauldron == recentlyRemovedCauldron || !cauldron.CanLockInEssenceSlot)
            return;

        lockedCauldron = cauldron;
        cauldron.LockToEssenceSlot(this, lockPoint);
    }

    public bool TryRemoveCauldron()
    {
        if (lockedCauldron == null || lockedCauldron.IsBeingFilled)
            return false;

        Cauldron cauldron = lockedCauldron;
        lockedCauldron = null;
        recentlyRemovedCauldron = cauldron;
        cauldron.UnlockFromEssenceSlot();
        return true;
    }
}
