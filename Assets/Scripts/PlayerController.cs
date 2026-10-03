using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Mouse Look")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 80f;
    [Tooltip("Smooths small mouse-input steps. Set to 0 for raw mouse look.")]
    [SerializeField, Range(0f, 0.15f)] private float mouseSmoothing = 0.03f;

    private CharacterController controller;
    private Vector3 velocity;
    private float cameraPitch;
    private Vector2 smoothedMouseDelta;
    private Vector2 mouseSmoothVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMovement();
        HandleMouseLook();
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                input.y += 1;

            if (Keyboard.current.sKey.isPressed)
                input.y -= 1;

            if (Keyboard.current.dKey.isPressed)
                input.x += 1;

            if (Keyboard.current.aKey.isPressed)
                input.x -= 1;
        }

        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        move = Vector3.ClampMagnitude(move, 1f);

        controller.Move(move * moveSpeed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null || cameraTransform == null)
            return;

        Vector2 rawMouseDelta = Mouse.current.delta.ReadValue();
        smoothedMouseDelta = mouseSmoothing > 0f
            ? Vector2.SmoothDamp(
                smoothedMouseDelta,
                rawMouseDelta,
                ref mouseSmoothVelocity,
                mouseSmoothing)
            : rawMouseDelta;

        float mouseX = smoothedMouseDelta.x * mouseSensitivity;
        float mouseY = smoothedMouseDelta.y * mouseSensitivity;

        // Turn player left/right
        transform.Rotate(Vector3.up * mouseX);

        // Look up/down
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}
