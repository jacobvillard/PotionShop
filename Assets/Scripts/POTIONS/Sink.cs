using UnityEngine;

public class Sink : MonoBehaviour
{
    [SerializeField] private Transform lockPoint;

    private ISinkable lockedObject;

    public ISinkable LockedObject => lockedObject;

    public bool HasLockedObject => lockedObject != null;

    public bool IsFilling { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        TryLockObject(other);
    }

    private void TryLockObject(Collider other)
    {
        if (HasLockedObject)
            return;

        if (IsFilling)
            return;

        ISinkable sinkable =
            other.GetComponentInParent<ISinkable>();

        if (sinkable == null)
            return;

        // Only empty containers that are not currently being carried should
        // snap into the sink.
        if (!sinkable.CanLockInSink)
            return;

        LockObject(sinkable);
    }

    private void LockObject(ISinkable sinkable)
    {
        lockedObject = sinkable;

        sinkable.SetSink(this);
        sinkable.LockToSink(lockPoint);
    }

    public bool TryRemoveObject()
    {
        if (lockedObject == null)
            return false;

        if (IsFilling)
            return false;

        if (!lockedObject.CanBeRemovedFromSink)
            return false;

        ISinkable objectToRemove = lockedObject;

        // Sink is now considered empty.
        lockedObject = null;

        objectToRemove.UnlockFromSink();

        return true;
    }

    public void BeginFill()
    {
        IsFilling = true;
    }

    public void EndFill()
    {
        IsFilling = false;
    }
}
