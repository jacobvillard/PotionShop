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

    private Rigidbody heldRigidbody;
    private ISinkable heldSinkable;
    private IHoldable heldHoldable;
    private ObjectSfx heldObjectSfx;
    private Quaternion heldRotation;
    private Collider[] playerColliders;
    private readonly List<Collider> ignoredHeldColliders = new();
    private bool heldObjectKeepsPlayerCollisions;

    private float originalDrag;
    private float originalAngularDrag;
    private bool isPotionLayer;
    private bool isItemLayer;
    
    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        playerColliders = GetComponentsInParent<Collider>(true);
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
        heldRigidbody = rb;
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
        
        Quaternion targetRotation;

        if (isPotionLayer)
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
                rotationDamping;

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
