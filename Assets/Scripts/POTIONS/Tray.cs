using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Secures cookable items when they land on an upright tray. Rotate the tray
/// away from its configured rest rotation (for example, flip it over) to drop everything.
/// </summary>
public class Tray : MonoBehaviour
{
    [Header("Orientation")]
    [Tooltip("The tray's expected local rotation when it can hold items.")]
    [SerializeField] private Vector3 uprightLocalEulerAngles = new Vector3(-90f, 0f, 0f);
    [SerializeField, Range(0f, 90f)] private float rotationTolerance = 20f;

    [Header("Pickup")]
    [Tooltip("Seconds attached items are protected from dropping after the player picks up this tray.")]
    [SerializeField, Min(0f)] private float pickupDropProtectionDuration = 0.3f;

    [Header("While Held")]
    [Tooltip("Multiplies the player's correction toward this tray's configured upright rotation while it is carried.")]
    [SerializeField, Min(1f)] private float heldRotationStrength = 2f;

    [Header("Attachment Collider")]
    [Tooltip("Assign a BoxCollider on a child GameObject. You control its size, trigger setting, and layer.")]
    [SerializeField] private BoxCollider attachmentCollider;

    [Header("Attached Cookables")]
    [Tooltip("Cookable GameObjects currently secured to this tray. This list is maintained at runtime for inspection.")]
    [SerializeField] private List<GameObject> attachedGameObjects = new List<GameObject>();

    private readonly HashSet<ICookable> cookables = new HashSet<ICookable>();
    private Quaternion uprightLocalRotation;
    private float dropProtectionEndTime;

    public bool IsUpright => Quaternion.Angle(transform.localRotation, uprightLocalRotation) <= rotationTolerance;
    public IReadOnlyList<GameObject> AttachedGameObjects => attachedGameObjects;
    public float HeldRotationStrength => heldRotationStrength;

    public Quaternion GetConfiguredWorldRotation()
    {
        Quaternion configuredLocalRotation = Quaternion.Euler(uprightLocalEulerAngles);
        return transform.parent != null
            ? transform.parent.rotation * configuredLocalRotation
            : configuredLocalRotation;
    }

    private void Awake()
    {
        InitialiseContactVolume();
        uprightLocalRotation = Quaternion.Euler(uprightLocalEulerAngles);
    }

    private void LateUpdate()
    {
        if (Time.time >= dropProtectionEndTime && !IsUpright)
            RemoveAllCookables();
    }

    /// <summary>Prevents attached items from being dropped for a short pickup grace period.</summary>
    public void NotifyPickedUp()
    {
        dropProtectionEndTime = Mathf.Max(
            dropProtectionEndTime,
            Time.time + pickupDropProtectionDuration);
    }

    public void TryPlaceCookable(Collider other)
    {
        if (!IsUpright)
            return;

        ICookable cookable = FindCookable(other);
        if (cookable != null && cookable.PlaceOnTray(this))
        {
            cookables.Add(cookable);
            TrackCookable(cookable);
        }
    }

    private static ICookable FindCookable(Collider collider)
    {
        foreach (MonoBehaviour behaviour in collider.GetComponentsInParent<MonoBehaviour>())
        {
            if (behaviour is ICookable cookable)
                return cookable;
        }

        return null;
    }

    public void NotifyCookableRemoved(ICookable cookable)
    {
        cookables.Remove(cookable);
        if (cookable is Component component)
            attachedGameObjects.Remove(component.gameObject);
    }

    /// <summary>Disables or restores physical collisions between this tray and a secured cookable.</summary>
    public void SetCookableCollisions(Cookable cookable, bool ignored)
    {
        if (cookable == null)
            return;

        foreach (Collider cookableCollider in cookable.GetComponentsInChildren<Collider>(true))
        {
            foreach (Collider trayCollider in GetComponentsInChildren<Collider>(true))
            {
                if (trayCollider == null || IsCookableCollider(trayCollider))
                    continue;

                Physics.IgnoreCollision(cookableCollider, trayCollider, ignored);
            }
        }
    }

    private void RemoveAllCookables()
    {
        if (cookables.Count == 0)
            return;

        ICookable[] attachedCookables = new ICookable[cookables.Count];
        cookables.CopyTo(attachedCookables);
        foreach (ICookable cookable in attachedCookables)
            cookable?.RemoveFromTray();
    }

    private void TrackCookable(ICookable cookable)
    {
        if (cookable is Component component && !attachedGameObjects.Contains(component.gameObject))
            attachedGameObjects.Add(component.gameObject);
    }

    private static bool IsCookableCollider(Collider collider)
    {
        foreach (MonoBehaviour behaviour in collider.GetComponentsInParent<MonoBehaviour>())
        {
            if (behaviour is ICookable)
                return true;
        }

        return false;
    }

    private void OnDisable()
    {
        RemoveAllCookables();
        attachedGameObjects.Clear();
    }

    private void OnValidate()
    {
        uprightLocalRotation = Quaternion.Euler(uprightLocalEulerAngles);
    }

    private void InitialiseContactVolume()
    {
        if (attachmentCollider == null)
        {
            foreach (BoxCollider childCollider in GetComponentsInChildren<BoxCollider>(true))
            {
                if (childCollider.transform != transform)
                {
                    attachmentCollider = childCollider;
                    break;
                }
            }
        }

        if (attachmentCollider == null)
            return;

        TrayContactVolume contactVolume = attachmentCollider.GetComponent<TrayContactVolume>();
        if (contactVolume == null)
            contactVolume = attachmentCollider.gameObject.AddComponent<TrayContactVolume>();

        contactVolume.Initialise(this);
    }
}

/// <summary>Forwards contacts from a tray's child BoxCollider to its parent tray.</summary>
[RequireComponent(typeof(BoxCollider))]
public class TrayContactVolume : MonoBehaviour
{
    private Tray tray;

    public void Initialise(Tray owner)
    {
        tray = owner;
    }

    private void Awake()
    {
        if (tray == null)
            tray = GetComponentInParent<Tray>();
    }

    private void OnTriggerEnter(Collider other)
    {
        tray?.TryPlaceCookable(other);
    }

    private void OnTriggerStay(Collider other)
    {
        tray?.TryPlaceCookable(other);
    }
}
