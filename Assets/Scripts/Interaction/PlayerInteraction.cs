using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactionMask = ~0;

    [Header("Object Holding")]
    [SerializeField] private float holdDistance = 0.5f;
    [SerializeField] private float holdForce = 50f;
    [SerializeField] private float maxHoldSpeed = 10f;
    [SerializeField] private float rotationDamping = 10f;

    [Header("Default Pickup / Drop SFX")]
    [Tooltip("Used only when the picked-up Rigidbody has no ObjectSfx component.")]
    [SerializeField] private AudioClip defaultPickupSfx;
    [SerializeField] private AudioClip defaultDropSfx;
    [SerializeField, Range(0f, 1f)] private float defaultPickupSfxVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float defaultDropSfxVolume = 0.7f;

    [Header("Plain Prop Return")]
    [Tooltip("World-space speed used when returning a plain prop to its saved position.")]
    [SerializeField, Min(0.01f)] private float propReturnSpeed = 12f;
    [Tooltip("World-space speed used for long, trail-visible floating returns.")]
    [SerializeField, Min(0.01f)] private float propReturnFloatingSpeed = 2f;
    [Tooltip("Height of the floating arc used only for trail-visible returns.")]
    [SerializeField, Min(0f)] private float propReturnArcHeight = 0.5f;
    [SerializeField, Min(0f)] private float propReturnDelay = 1.25f;
    [SerializeField, Min(0.01f)] private float propReturnDistance = 0.15f;
    [Tooltip("A trail appears only when the prop returns from at least this far away.")]
    [SerializeField, Min(0f)] private float propReturnTrailMinDistance = 0.5f;
    [SerializeField] private Color propReturnTrailColour = new Color(0.05f, 0.25f, 1f, 0.9f);

    private Rigidbody heldRigidbody;
    private ISinkable heldSinkable;
    private IHoldable heldHoldable;
    private ObjectSfx heldObjectSfx;
    private Quaternion heldRotation;
    private Collider[] playerColliders;
    private readonly List<Collider> ignoredHeldColliders = new();
    private readonly Dictionary<Rigidbody, ReturnOrigin> returnOrigins = new();
    private readonly Dictionary<Rigidbody, float> displacedSince = new();
    private readonly HashSet<Rigidbody> returningProps = new();
    private readonly List<Rigidbody> registeredProps = new();
    private bool heldObjectKeepsPlayerCollisions;

    private float originalDrag;
    private float originalAngularDrag;
    private bool isPotionLayer;
    private bool isItemLayer;

    private struct ReturnOrigin
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }
    
    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        playerColliders = GetComponentsInParent<Collider>(true);
    }

    private void Start()
    {
        foreach (Rigidbody rb in FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            RecordReturnOriginIfEligible(rb);
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if (Keyboard.current == null)
            return;

        if (SettingsManager.InteractPressed())
        {
            if (heldRigidbody != null)
            {
                DropObject();
            }
            else
            {
                TryInteract();
            }
        }
        
        if (heldRigidbody != null && SettingsManager.UsePressed())
        {
            IUseable useable =
                heldRigidbody.GetComponentInParent<IUseable>();

            if (useable != null)
            {
                useable.Use();
            }
        }

        CheckForDisplacedProps();
    }

    private void FixedUpdate()
    {
        // A jar can be placed directly while held. Once its slot catches it,
        // stop the carry controller from trying to move its kinematic body.
        if (heldHoldable is IngredientJar jar && jar.IsLocked)
        {
            ReleaseHeldObject();
            return;
        }

        // Like jars, cauldrons may be inserted directly from the player's
        // hands into an essence extractor slot.
        if (heldSinkable is Cauldron cauldron && cauldron.IsLockedInEssenceSlot)
        {
            ReleaseHeldObject();
            return;
        }

        if (heldRigidbody != null)
        {
            MoveHeldObject();
        }
    }

    private void TryInteract()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactDistance,
                interactionMask,
                QueryTriggerInteraction.Collide))
        {
            return;
        }

        // Get both BEFORE interacting.
        IInteractable interactable =
            hit.collider.GetComponentInParent<IInteractable>();

        // A calibrated extractor handle must be explicitly released when the
        // player presses Interact anywhere else. If the same handle is pressed
        // again, release it without immediately re-arming it.
        EssenceNeedleController activeNeedle = EssenceNeedleController.ActiveController;
        if (activeNeedle != null)
        {
            activeNeedle.CancelControl();
            if (ReferenceEquals(interactable, activeNeedle))
                return;
        }

        Rigidbody rb =
            hit.collider.attachedRigidbody;

        // Mark a sinkable as held before unlocking it. Changing its parent can
        // synchronously generate trigger events, so this must happen before
        // Interact() releases it from the sink.
        ISinkable sinkable =
            hit.collider.GetComponentInParent<ISinkable>();

        IHoldable holdable =
            hit.collider.GetComponentInParent<IHoldable>();

        bool markedForPickup =
            heldRigidbody == null && sinkable != null;

        if (markedForPickup)
            sinkable.SetIsHeld(true);

        bool markedHoldableForPickup =
            heldRigidbody == null && holdable != null;

        if (markedHoldableForPickup)
            holdable.SetIsHeld(true);

        // --------------------------------------------------
        // INTERACT FIRST
        // --------------------------------------------------

        if (interactable != null)
        {
            interactable.Interact();
        }

        // --------------------------------------------------
        // THEN TRY PICKUP
        // --------------------------------------------------

        // Important:
        // Interact() may have just changed this Rigidbody
        // from kinematic -> non-kinematic.
        if (rb != null &&
            !rb.isKinematic &&
            heldRigidbody == null)
        {
            PickupObject(rb);
        }
        else if (markedForPickup)
        {
            // The interaction did not result in a pickup (for example, the
            // cauldron is still filling), so restore its normal lock state.
            sinkable.SetIsHeld(false);
        }

        if (markedHoldableForPickup && heldRigidbody == null)
            holdable.SetIsHeld(false);
    }

    private void PickupObject(Rigidbody rb)
    {
        RecordReturnOriginIfEligible(rb);
        heldRigidbody = rb;
        rb.GetComponentInParent<Tray>()?.NotifyPickedUp();
        // Staff01 is intentionally allowed to retain its collision with the
        // player while held. Other carryable physics objects ignore the player
        // so they cannot block forward movement.
        heldObjectKeepsPlayerCollisions = rb.GetComponentInParent<Staff>() != null;
        if (!heldObjectKeepsPlayerCollisions)
            IgnoreHeldObjectPlayerCollisions();
        heldSinkable = rb.GetComponentInParent<ISinkable>();
        heldSinkable?.SetIsHeld(true);

        heldHoldable = rb.GetComponentInParent<IHoldable>();
        heldHoldable?.SetIsHeld(true);

        heldObjectSfx = rb.GetComponentInParent<ObjectSfx>();
        if (heldObjectSfx != null)
            heldObjectSfx.PlayPickup();
        else
            SfxAudio.PlayAt(defaultPickupSfx, rb.position, defaultPickupSfxVolume);

        originalDrag = rb.linearDamping;
        originalAngularDrag = rb.angularDamping;

        rb.linearDamping = 8f;
        rb.angularDamping = 8f;
        
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        heldRotation = Quaternion.Inverse(playerCamera.transform.rotation) * rb.rotation;
        
        var layer = rb.gameObject.layer;
        isPotionLayer = layer == LayerMask.NameToLayer("Potion");
        isItemLayer = layer == LayerMask.NameToLayer("Items");
  
        rb.useGravity = true;
    }

    private void MoveHeldObject()
    {
        Vector3 targetPosition =
            playerCamera.transform.position +
            playerCamera.transform.forward * holdDistance;

        Vector3 difference =
            targetPosition - heldRigidbody.position;

        Vector3 desiredVelocity =
            difference * holdForce;

        desiredVelocity =
            Vector3.ClampMagnitude(
                desiredVelocity,
                maxHoldSpeed
            );

        Vector3 velocityDifference =
            desiredVelocity - heldRigidbody.linearVelocity;

        heldRigidbody.AddForce(
            velocityDifference,
            ForceMode.VelocityChange
        );
        
        Tray heldTray = heldRigidbody.GetComponentInParent<Tray>();
        Quaternion targetRotation;

        if (heldTray != null)
        {
            // A held tray gently corrects itself toward its configured level
            // rotation instead of preserving any accidental pickup tilt.
            targetRotation = heldTray.GetConfiguredWorldRotation();
        }
        else if (isPotionLayer)
        {
            targetRotation =
                playerCamera.transform.rotation *
                Quaternion.Euler(-90f, 0f, 0f);
        }
        else if (isItemLayer)
        {
            targetRotation =
                playerCamera.transform.rotation *
                Quaternion.Euler(0f, 0f, 0f);
        }
        else
        {
            targetRotation =
                playerCamera.transform.rotation *
                heldRotation;
        }
        
        Quaternion rotationDifference =
            targetRotation *
            Quaternion.Inverse(heldRigidbody.rotation);

        rotationDifference.ToAngleAxis(
            out float angle,
            out Vector3 axis
        );

        if (angle > 180f)
            angle -= 360f;

        if (axis.sqrMagnitude > 0.001f)
        {
            Vector3 torque =
                axis.normalized *
                angle *
                Mathf.Deg2Rad *
            rotationDamping * (heldTray != null ? heldTray.HeldRotationStrength : 1f);

            heldRigidbody.AddTorque(
                torque,
                ForceMode.Acceleration
            );
        }
    }

    private void DropObject()
    {
        ReleaseHeldObject();
    }

    private void ReleaseHeldObject()
    {
        Rigidbody releasedRigidbody = heldRigidbody;
        ReturnOrigin returnOrigin = default;
        bool shouldReturn = releasedRigidbody != null &&
                            returnOrigins.TryGetValue(releasedRigidbody, out returnOrigin);

        if (heldObjectSfx != null)
            heldObjectSfx.PlayDrop();
        else if (heldRigidbody != null)
            SfxAudio.PlayAt(defaultDropSfx, heldRigidbody.position, defaultDropSfxVolume);

        heldSinkable?.SetIsHeld(false);
        heldSinkable = null;

        heldHoldable?.SetIsHeld(false);
        heldHoldable = null;
        heldObjectSfx = null;

        RestoreHeldObjectPlayerCollisions();
        heldObjectKeepsPlayerCollisions = false;

        heldRigidbody.linearDamping = originalDrag;
        heldRigidbody.angularDamping = originalAngularDrag;

        heldRigidbody = null;

        if (shouldReturn)
            BeginReturningProp(releasedRigidbody, returnOrigin);
    }

    private void BeginReturningProp(Rigidbody rb, ReturnOrigin origin)
    {
        if (rb == null || !returningProps.Add(rb))
            return;

        displacedSince.Remove(rb);
        StartCoroutine(ReturnPlainProp(rb, origin));
    }

    private void RecordReturnOriginIfEligible(Rigidbody rb)
    {
        if (rb == null || returnOrigins.ContainsKey(rb) || !IsPlainReturnableProp(rb))
            return;

        returnOrigins.Add(rb, new ReturnOrigin
        {
            Position = rb.position,
            Rotation = rb.rotation
        });
    }

    private void CheckForDisplacedProps()
    {
        registeredProps.Clear();
        registeredProps.AddRange(returnOrigins.Keys);

        foreach (Rigidbody rb in registeredProps)
        {
            if (rb == null)
            {
                returnOrigins.Remove(rb);
                displacedSince.Remove(rb);
                continue;
            }

            if (rb == heldRigidbody || returningProps.Contains(rb))
                continue;

            ReturnOrigin origin = returnOrigins[rb];
            if (Vector3.Distance(rb.position, origin.Position) <= propReturnDistance)
            {
                displacedSince.Remove(rb);
                continue;
            }

            if (!displacedSince.TryGetValue(rb, out float displacedTime))
            {
                displacedSince.Add(rb, Time.time);
                continue;
            }

            if (Time.time - displacedTime >= propReturnDelay)
                BeginReturningProp(rb, origin);
        }
    }

    private bool IsPlainReturnableProp(Rigidbody rb)
    {
        int layer = rb.gameObject.layer;
        if (layer == LayerMask.NameToLayer("Potion") || layer == LayerMask.NameToLayer("Items"))
            return false;

        foreach (MonoBehaviour behaviour in rb.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null)
                return false;
        }

        return true;
    }

    private System.Collections.IEnumerator ReturnPlainProp(Rigidbody rb, ReturnOrigin origin)
    {
        if (rb == null)
            yield break;

        bool originalKinematic = rb.isKinematic;
        bool originalUseGravity = rb.useGravity;
        bool originalDetectCollisions = rb.detectCollisions;
        RigidbodyInterpolation originalInterpolation = rb.interpolation;
        Vector3 startPosition = rb.position;
        Quaternion startRotation = rb.rotation;
        float distance = Vector3.Distance(startPosition, origin.Position);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;
        rb.interpolation = RigidbodyInterpolation.None;

        Material trailMaterial = null;
        TrailRenderer returnTrail = null;
        if (distance >= propReturnTrailMinDistance)
            returnTrail = CreateReturnTrail(rb, out trailMaterial);

        float returnSpeed = returnTrail != null ? propReturnFloatingSpeed : propReturnSpeed;
        float returnDuration = distance / returnSpeed;
        float elapsed = 0f;

        while (elapsed < returnDuration && rb != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnDuration);
            Vector3 position = Vector3.Lerp(startPosition, origin.Position, t);
            if (returnTrail != null)
                position += Vector3.up * Mathf.Sin(t * Mathf.PI) * propReturnArcHeight;
            rb.position = position;
            rb.rotation = Quaternion.Slerp(startRotation, origin.Rotation, t);
            yield return null;
        }

        if (rb == null)
        {
            returningProps.Remove(rb);
            DestroyReturnTrail(returnTrail, trailMaterial);
            yield break;
        }

        // Set the exact final transform after the linear return completes.
        rb.position = origin.Position;
        rb.rotation = origin.Rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = originalKinematic;
        rb.useGravity = originalUseGravity;
        rb.detectCollisions = originalDetectCollisions;
        rb.interpolation = originalInterpolation;
        returningProps.Remove(rb);
        DestroyReturnTrail(returnTrail, trailMaterial);
    }

    private TrailRenderer CreateReturnTrail(Rigidbody rb, out Material trailMaterial)
    {
        trailMaterial = null;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        TrailRenderer trail = rb.gameObject.AddComponent<TrailRenderer>();
        trailMaterial = new Material(shader);
        trail.sharedMaterial = trailMaterial;
        trail.time = 0.25f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = 0.06f;
        trail.endWidth = 0f;
        trail.startColor = propReturnTrailColour;
        trail.endColor = new Color(
            propReturnTrailColour.r,
            propReturnTrailColour.g,
            propReturnTrailColour.b,
            0f);
        return trail;
    }

    private static void DestroyReturnTrail(TrailRenderer trail, Material trailMaterial)
    {
        if (trail != null)
            Destroy(trail);
        if (trailMaterial != null)
            Destroy(trailMaterial);
    }

    private void IgnoreHeldObjectPlayerCollisions()
    {
        if (heldRigidbody == null)
            return;

        ignoredHeldColliders.Clear();
        foreach (Collider heldCollider in heldRigidbody.GetComponentsInChildren<Collider>(true))
        {
            if (heldCollider == null)
                continue;

            ignoredHeldColliders.Add(heldCollider);
            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null)
                    Physics.IgnoreCollision(heldCollider, playerCollider, true);
            }
        }
    }

    private void RestoreHeldObjectPlayerCollisions()
    {
        foreach (Collider heldCollider in ignoredHeldColliders)
        {
            if (heldCollider == null)
                continue;

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null)
                    Physics.IgnoreCollision(heldCollider, playerCollider, false);
            }
        }

        ignoredHeldColliders.Clear();
    }
}
