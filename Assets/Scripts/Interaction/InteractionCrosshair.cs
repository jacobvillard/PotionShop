using UnityEngine;
using UnityEngine.UI;

public class InteractionCrosshair : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [SerializeField] private RectTransform crosshairPrimary;
    [SerializeField] private Image crosshairPrimaryImage;

    [SerializeField] private Image crosshairSecondaryImage;

    [Header("Detection")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactionMask = ~0;

    [Header("Rotation")]
    [SerializeField] private float hoverRotationZ = -45f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Colours")]
    [SerializeField] private Color normalColour = Color.white;
    [SerializeField] private Color interactableColour = Color.green;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
        bool hovering = CheckForInteractable();

        float targetZ = hovering
            ? hoverRotationZ
            : 0f;

        Quaternion targetRotation =
            Quaternion.Euler(0f, 0f, targetZ);

        // Only primary rotates
        if (crosshairPrimary != null)
        {
            crosshairPrimary.localRotation =
                Quaternion.Lerp(
                    crosshairPrimary.localRotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
        }

        Color targetColour = hovering
            ? interactableColour
            : normalColour;

        if (crosshairPrimaryImage != null)
            crosshairPrimaryImage.color = targetColour;

        if (crosshairSecondaryImage != null)
            crosshairSecondaryImage.color = targetColour;
    }

    private bool CheckForInteractable()
    {
        if (playerCamera == null)
            return false;

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
            return false;
        }

        IInteractable interactable =
            hit.collider.GetComponentInParent<IInteractable>();

        if (interactable != null)
            return true;

        Rigidbody rb = hit.collider.attachedRigidbody;

        if (rb != null && !rb.isKinematic)
            return true;

        return false;
    }
}
