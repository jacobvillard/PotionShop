using UnityEngine;

public class DripJarSlot : MonoBehaviour
{
    [SerializeField] private Transform lockPoint;
    [SerializeField, Min(0.01f)] private float removalRearmDistance = 0.5f;

    private IngredientJar lockedJar;
    private IngredientJar recentlyRemovedJar;

    public IngredientJar LockedJar => lockedJar;
    public bool HasLockedJar => lockedJar != null;

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
        jar.LockToSlot(this, lockPoint);
    }

    public bool TryRemoveJar()
    {
        if (lockedJar == null)
            return false;

        IngredientJar jarToRemove = lockedJar;
        lockedJar = null;
        recentlyRemovedJar = jarToRemove;
        jarToRemove.UnlockFromSlot();
        return true;
    }
}
