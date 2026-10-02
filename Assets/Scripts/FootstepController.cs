using System.Collections.Generic;
using UnityEngine;

public class FootstepController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private AudioSource footstepSource;

    [Header("Timing")]
    [SerializeField] private float stepInterval = 0.5f;
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("Surface Detection")]
    [SerializeField] private float rayDistance = 2f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Stone")]
    [SerializeField] private List<AudioClip> stoneClips = new();

    [Header("Wood")]
    [SerializeField] private List<AudioClip> woodClips = new();

    [Header("Audio")]
    [SerializeField] private float minVolume = 0.85f;
    [SerializeField] private float maxVolume = 1f;

    [SerializeField] private float minPitch = 0.95f;
    [SerializeField] private float maxPitch = 1.05f;

    private float stepTimer;
    private Vector3 lastPosition;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (footstepSource == null)
            footstepSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (controller == null || footstepSource == null)
            return;

        // Work out how far the player actually moved this frame
        Vector3 movement = transform.position - lastPosition;

        // Ignore vertical movement
        movement.y = 0f;

        bool moving =
            movement.magnitude >
            movementThreshold * Time.deltaTime;

        lastPosition = transform.position;

        // Don't play footsteps while standing still
        if (!moving)
        {
            stepTimer = 0f;
            return;
        }

        // Don't play footsteps while airborne
        if (!controller.isGrounded)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer += Time.deltaTime;

        if (stepTimer >= stepInterval)
        {
            PlayFootstep();
            stepTimer = 0f;
        }
    }

    private void PlayFootstep()
    {
        Vector3 rayOrigin =
            transform.position +
            Vector3.up * 0.2f;

        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        List<AudioClip> clips = null;

        if (hit.collider.CompareTag("Stone"))
        {
            clips = stoneClips;
        }
        else if (hit.collider.CompareTag("Wood"))
        {
            clips = woodClips;
        }

        if (clips == null || clips.Count == 0)
            return;

        AudioClip clip =
            clips[Random.Range(0, clips.Count)];

        footstepSource.pitch =
            Random.Range(minPitch, maxPitch);

        footstepSource.volume =
            Random.Range(minVolume, maxVolume);

        footstepSource.PlayOneShot(clip);
    }
}
