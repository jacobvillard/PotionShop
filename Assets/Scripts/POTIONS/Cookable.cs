using UnityEngine;

/// <summary>
/// Makes a Rigidbody item placeable on a <see cref="Tray"/>. Interacting with
/// an item on a tray releases it before PlayerInteraction performs its normal pickup.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Cookable : MonoBehaviour, ICookable, IInteractable, IHoldable
{
    [SerializeField] private Rigidbody itemRigidbody;

    private Transform originalParent;
    private Tray currentTray;
    private bool originalKinematic;
    private bool originalUseGravity;
    private RigidbodyInterpolation originalInterpolation;
    private bool isHeld;

    public bool IsOnTray => currentTray != null;
    public bool CanBePlacedOnTray => !IsOnTray && !isHeld && itemRigidbody != null;

    private void Awake()
    {
        if (itemRigidbody == null)
            itemRigidbody = GetComponent<Rigidbody>();

        originalParent = transform.parent;
    }

    public bool PlaceOnTray(Tray tray)
    {
        if (tray == null || !CanBePlacedOnTray || !tray.IsUpright)
            return false;

        currentTray = tray;
        originalKinematic = itemRigidbody.isKinematic;
        originalUseGravity = itemRigidbody.useGravity;
        originalInterpolation = itemRigidbody.interpolation;
        itemRigidbody.linearVelocity = Vector3.zero;
        itemRigidbody.angularVelocity = Vector3.zero;
        itemRigidbody.isKinematic = true;
        itemRigidbody.useGravity = false;
        // A kinematic child Rigidbody with interpolation enabled renders one
        // physics tick behind its moving parent, making it appear to trail.
        itemRigidbody.interpolation = RigidbodyInterpolation.None;
        tray.SetCookableCollisions(this, true);
        transform.SetParent(tray.transform, true);

        return true;
    }

    public void RemoveFromTray()
    {
        if (currentTray == null)
            return;

        Tray tray = currentTray;
        currentTray = null;
        tray.SetCookableCollisions(this, false);
        transform.SetParent(originalParent, true);

        itemRigidbody.isKinematic = originalKinematic;
        itemRigidbody.useGravity = originalUseGravity;
        itemRigidbody.interpolation = originalInterpolation;
        itemRigidbody.linearVelocity = Vector3.zero;
        itemRigidbody.angularVelocity = Vector3.zero;
        tray.NotifyCookableRemoved(this);
    }

    public void Interact()
    {
        RemoveFromTray();
    }

    /// <summary>Called by PlayerInteraction so held items cannot snap onto a tray.</summary>
    public void SetIsHeld(bool held)
    {
        isHeld = held;
    }

    private void OnDisable()
    {
        RemoveFromTray();
    }
}
