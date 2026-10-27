using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loops two book halves between poses captured from their local transforms.
/// Capture both poses in edit mode using this component's context menu.
/// </summary>
public class MimicBookTest : MonoBehaviour, IStaffable
{
    [Serializable]
    private class LocalPose
    {
        public Vector3 position;
        public Vector3 rotation;

        public void Capture(Transform target)
        {
            position = target.localPosition;
            rotation = target.localEulerAngles;
        }
    }

    [Serializable]
    private class BookHalf
    {
        public Transform target;
        public LocalPose start = new LocalPose();
        public LocalPose end = new LocalPose();
    }

    [Header("Book Halves")]
    [SerializeField] private BookHalf upperHalf = new BookHalf();
    [SerializeField] private BookHalf lowerHalf = new BookHalf();

    [Header("Animation")]
    [Tooltip("Time in seconds to travel from one pose to the other.")]
    [SerializeField, Min(0.01f)] private float movementDuration = 0.3f;
    [SerializeField, Min(0f)] private float holdAtStart = 0.15f;
    [SerializeField, Min(0f)] private float holdAtEnd = 0.1f;
    [SerializeField] private bool animateOnPlay = true;

    [Header("Flying")]
    [SerializeField] private bool flyAround = true;
    [SerializeField, Min(0f)] private float flightSpeed = 2f;
    [Tooltip("How strongly the mimic steers its velocity while flying.")]
    [SerializeField, Min(0f)] private float flightForce = 6f;
    [SerializeField, Min(0f)] private float turnSpeed = 6f;
    [SerializeField, Min(0f)] private float wanderRadius = 3f;
    [SerializeField, Min(0f)] private float hoverHeightVariation = 0.5f;
    [Tooltip("Extra rotation after aligning the book's local +X axis with its direction of travel.")]
    [SerializeField] private float facingYawOffset;
    [Tooltip("Approximate size of the mimic in world units, used for its runtime collider and wall checks.")]
    [SerializeField, Min(0.05f)] private float collisionRadius = 0.25f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Chasing and Chomping")]
    [SerializeField] private Transform player;
    [Tooltip("Target loose rigidbodies on any layer. Turn off to use Item Layers instead.")]
    [SerializeField] private bool targetAllRigidbodyLayers = true;
    [Tooltip("Layers to search when Target All Rigidbody Layers is disabled.")]
    [SerializeField] private LayerMask itemLayers = 1 << 6;
    [SerializeField, Min(0f)] private float detectionRadius = 5f;
    [Tooltip("Maximum time on a target, capped at three seconds.")]
    [SerializeField, Range(0.1f, 3f)] private float chaseDuration = 3f;
    [SerializeField, Min(0f)] private float breakBetweenChases = 0.3f;
    [SerializeField, Min(0f)] private float lungeStrength = 2f;
    [SerializeField, Min(0f)] private float lungeLift = 0.6f;
    [SerializeField, Min(0.1f)] private float lungeInterval = 1.2f;
    [Tooltip("Maximum distance from the bottom of the mimic to a supporting surface for lunges.")]
    [SerializeField, Min(0f)] private float groundLungeHeight = 0.75f;
    [Tooltip("Layers considered supporting surfaces for lunges.")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Min(0.1f)] private float biteDistance = 1f;
    [SerializeField, Min(0.1f)] private float biteInterval = 0.8f;
    [SerializeField, Min(0f)] private float itemNudgeImpulse = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float chompSpeedMultiplier = 0.45f;
    [Tooltip("Time spent chasing an item before it can be eaten, if it is within bite distance.")]
    [SerializeField, Range(0f, 2.9f)] private float eatAfterSeconds = 2.9f;
    [Tooltip("Give up when the mimic cannot make progress towards a target for this long.")]
    [SerializeField, Min(0.2f)] private float stuckTimeout = 1.2f;
    [Tooltip("How long to avoid a target that could not be reached.")]
    [SerializeField, Min(0f)] private float unreachableTargetCooldown = 8f;

    [Header("Sounds")]
    [SerializeField] private AudioSource soundSource;
    [Tooltip("Leave empty to use the included Throwing Books preview.")]
    [SerializeField] private AudioClip bookClosingSound;
    [SerializeField, Range(0f, 1f)] private float bookClosingVolume = 1f;
    [SerializeField, Min(0f)] private float bookClosingSoundDelay = 0f;
    [Tooltip("Turn off if your captured Start pose is the closed pose.")]
    [SerializeField] private bool endPoseIsClosed = true;
    [SerializeField] private AudioClip eatingSound;
    [SerializeField, Range(0f, 1f)] private float eatingVolume = 1f;

    [SerializeField, HideInInspector] private bool hasStartPose;
    [SerializeField, HideInInspector] private bool hasEndPose;
    private float elapsed;
    private Rigidbody body;
    private Vector3 homePosition;
    private Vector3 wanderDestination;
    private Transform chaseTarget;
    private Rigidbody targetBody;
    private readonly Dictionary<Transform, float> targetCooldowns = new Dictionary<Transform, float>();
    private float chaseEndsAt;
    private float eatAt;
    private float nextChaseAt;
    private float nextBiteAt;
    private float nextLungeAt;
    private float nextWanderAt;
    private float lastProgressAt;
    private float bestTargetDistance;
    private bool biting;

    private void Start()
    {
        if (bookClosingSound == null)
            bookClosingSound = Resources.Load<AudioClip>("Mimic/ThrowingBooks");
        if (soundSource == null)
        {
            soundSource = GetComponent<AudioSource>();
            if (soundSource == null)
            {
                soundSource = gameObject.AddComponent<AudioSource>();
                soundSource.spatialBlend = 1f;
            }
        }
        if (!flyAround) return;

        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        // Make a simple collider only when the book does not already have one.
        bool hasSolidCollider = false;
        foreach (Collider existingCollider in GetComponentsInChildren<Collider>())
        {
            if (existingCollider.enabled && !existingCollider.isTrigger)
            {
                hasSolidCollider = true;
                break;
            }
        }
        if (!hasSolidCollider)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            Vector3 scale = transform.lossyScale;
            float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            sphere.radius = collisionRadius / Mathf.Max(largestScale, 0.0001f);
        }

        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        homePosition = body.position;
        PickWanderDestination();
    }

    private void OnEnable()
    {
        elapsed = 0f;
        if (Application.isPlaying && hasStartPose && HasTargets())
            ApplyPose(0f);
    }

    private void Update()
    {
        if (!animateOnPlay || !hasStartPose || !hasEndPose || !HasTargets())
            return;

        float speedMultiplier = biting ? chompSpeedMultiplier : 1f;
        float duration = Mathf.Max(0.01f, movementDuration * speedMultiplier);
        float startHold = Mathf.Max(0f, holdAtStart * speedMultiplier);
        float endHold = Mathf.Max(0f, holdAtEnd * speedMultiplier);
        float cycleDuration = startHold + duration + endHold + duration;
        float previousElapsed = elapsed % cycleDuration;
        float nextElapsed = previousElapsed + Time.deltaTime;
        float closingPoint = endPoseIsClosed ? startHold + duration : cycleDuration;
        if (Time.deltaTime > 0f &&
            (previousElapsed < closingPoint && nextElapsed >= closingPoint ||
             nextElapsed >= cycleDuration && nextElapsed % cycleDuration >= closingPoint))
            PlayClosingSound();
        elapsed = nextElapsed % cycleDuration;

        float progress;
        if (elapsed < startHold)
            progress = 0f;
        else if (elapsed < startHold + duration)
            progress = (elapsed - startHold) / duration;
        else if (elapsed < startHold + duration + endHold)
            progress = 1f;
        else
            progress = 1f - (elapsed - startHold - duration - endHold) / duration;

        ApplyPose(Mathf.SmoothStep(0f, 1f, progress));
    }

    private void FixedUpdate()
    {
        if (!flyAround || body == null) return;

        if (targetBody != null && (targetBody.isKinematic ||
            (player != null && Vector3.Distance(targetBody.position, player.position) < 1.25f)))
            EndChase();

        if (targetBody != null && Time.time >= eatAt &&
            Vector3.Distance(body.position, targetBody.worldCenterOfMass) <= biteDistance &&
            HasLineOfSight(chaseTarget, targetBody.worldCenterOfMass))
            EatTarget();

        if (chaseTarget != null && Time.time >= chaseEndsAt)
            EndChase();

        if (chaseTarget == null && Time.time >= nextChaseAt)
            FindChaseTarget();

        Vector3 destination;
        if (chaseTarget != null)
        {
            destination = TargetPosition();
            float distance = Vector3.Distance(body.position, destination);
            bool visible = HasLineOfSight(chaseTarget, destination);
            biting = visible && distance <= biteDistance;
            if (biting || (visible && distance < bestTargetDistance - 0.1f))
            {
                bestTargetDistance = distance;
                lastProgressAt = Time.time;
            }
            if (Time.time - lastProgressAt >= Mathf.Max(0.2f, stuckTimeout))
            {
                EndChase(true);
                SteerVelocity(Vector3.zero);
                return;
            }
            if (biting)
            {
                if (Time.time >= nextBiteAt)
                    Bite();
                FaceDirection(destination - body.position);
                SteerVelocity(Vector3.zero);
                return;
            }

            if (Time.time >= nextLungeAt && IsNearGround() && !IsBlocked(destination - body.position))
            {
                Vector3 lunge = (destination - body.position).normalized * lungeStrength + Vector3.up * lungeLift;
                body.AddForce(lunge, ForceMode.VelocityChange);
                nextLungeAt = Time.time + Mathf.Max(0.1f, lungeInterval);
            }
        }
        else
        {
            biting = false;
            if (Time.time >= nextWanderAt || Vector3.Distance(body.position, wanderDestination) < 0.3f)
                PickWanderDestination();
            destination = wanderDestination;
        }

        Vector3 direction = destination - body.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            SteerVelocity(Vector3.zero);
            return;
        }
        FaceDirection(direction);
        if (IsBlocked(direction))
        {
            SteerVelocity(Vector3.zero);
            if (chaseTarget == null) nextWanderAt = Time.time;
        }
        else
            SteerVelocity(direction.normalized * Mathf.Min(flightSpeed, direction.magnitude * 2f));
    }

    private void FindChaseTarget()
    {
        bool playerInRange = player != null && player != transform &&
            !player.IsChildOf(transform) && !transform.IsChildOf(player) &&
            TargetAvailable(player) && Vector3.Distance(body.position, player.position) <= detectionRadius &&
            HasLineOfSight(player, player.position);
        Rigidbody item = FindNearbyItem();
        if (playerInRange && (item == null || UnityEngine.Random.value < 0.5f))
        {
            chaseTarget = player;
            targetBody = null;
        }
        else if (item != null)
        {
            chaseTarget = item.transform;
            targetBody = item;
        }

        if (chaseTarget != null)
        {
            chaseEndsAt = Time.time + Mathf.Clamp(chaseDuration, 0.1f, 3f);
            eatAt = Time.time + Mathf.Clamp(eatAfterSeconds, 0f, 2.9f);
            nextBiteAt = Time.time;
            nextLungeAt = Time.time;
            lastProgressAt = Time.time;
            bestTargetDistance = Vector3.Distance(body.position, TargetPosition());
        }
        else
            nextChaseAt = Time.time + 1f;
    }

    private Rigidbody FindNearbyItem()
    {
        int searchLayers = targetAllRigidbodyLayers ? ~0 : itemLayers.value;
        Collider[] nearby = Physics.OverlapSphere(body.position, detectionRadius, searchLayers, QueryTriggerInteraction.Ignore);
        Rigidbody chosen = null;
        float bestDistance = float.PositiveInfinity;
        foreach (Collider candidate in nearby)
        {
            Rigidbody item = candidate.attachedRigidbody;
            if (item == null || item == body || item.isKinematic ||
                candidate.transform.IsChildOf(transform) || item.transform.IsChildOf(transform) ||
                transform.IsChildOf(item.transform) || item.GetComponentInParent<MimicBookTest>() != null ||
                item.GetComponentInChildren<MimicBookTest>() != null ||
                !TargetAvailable(item.transform))
                continue;
            // Avoid interfering with objects the player may be holding.
            if (player != null && Vector3.Distance(item.position, player.position) < 1.25f)
                continue;
            if (!HasLineOfSight(item.transform, item.worldCenterOfMass))
                continue;
            float distance = (item.worldCenterOfMass - body.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                chosen = item;
                bestDistance = distance;
            }
        }
        return chosen;
    }

    private Vector3 TargetPosition()
    {
        if (targetBody != null) return targetBody.worldCenterOfMass;
        Collider targetCollider = chaseTarget.GetComponent<Collider>();
        return targetCollider != null ? targetCollider.bounds.center : chaseTarget.position;
    }

    private void Bite()
    {
        nextBiteAt = Time.time + Mathf.Max(0.1f, biteInterval);
        if (targetBody != null && !targetBody.isKinematic)
        {
            Vector3 nudge = (targetBody.worldCenterOfMass - body.position).normalized + Vector3.up * 0.35f;
            targetBody.AddForce(nudge.normalized * itemNudgeImpulse, ForceMode.Impulse);
        }
    }

    private void EatTarget()
    {
        if (targetBody == null || targetBody == body || targetBody.transform.IsChildOf(transform) ||
            transform.IsChildOf(targetBody.transform) || targetBody.GetComponentInParent<MimicBookTest>() != null ||
            targetBody.GetComponentInChildren<MimicBookTest>() != null)
        {
            EndChase(true);
            return;
        }
        GameObject eatenItem = targetBody.gameObject;
        if (soundSource != null && eatingSound != null)
            soundSource.PlayOneShot(eatingSound, eatingVolume);
        EndChase();
        targetCooldowns.Remove(eatenItem.transform);
        Destroy(eatenItem);
    }

    private void PlayClosingSound()
    {
        if (soundSource == null || bookClosingSound == null) return;
        if (bookClosingSoundDelay <= 0f)
            soundSource.PlayOneShot(bookClosingSound, bookClosingVolume);
        else
            StartCoroutine(PlayClosingSoundAfterDelay(bookClosingSound, bookClosingVolume, bookClosingSoundDelay));
    }

    private IEnumerator PlayClosingSoundAfterDelay(AudioClip clip, float volume, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isActiveAndEnabled && soundSource != null && clip != null)
            soundSource.PlayOneShot(clip, volume);
    }

    public void GetStaffedBiatch()
    {
        if (!Application.isPlaying) return;
        Destroy(gameObject);
    }

    private void EndChase(bool unreachable = false)
    {
        if (chaseTarget != null)
            targetCooldowns[chaseTarget] = Time.time + (unreachable
                ? Mathf.Max(0f, unreachableTargetCooldown)
                : Mathf.Max(1f, breakBetweenChases));
        chaseTarget = null;
        targetBody = null;
        biting = false;
        nextChaseAt = Time.time + Mathf.Max(0f, breakBetweenChases);
        PickWanderDestination();
    }

    private bool TargetAvailable(Transform target)
    {
        if (!targetCooldowns.TryGetValue(target, out float retryTime)) return true;
        if (Time.time < retryTime) return false;
        targetCooldowns.Remove(target);
        return true;
    }

    private bool HasLineOfSight(Transform target, Vector3 targetPosition)
    {
        if (target == null) return false;
        Vector3 direction = targetPosition - body.position;
        float distance = direction.magnitude;
        if (distance < 0.001f) return true;
        RaycastHit[] hits = Physics.RaycastAll(body.position, direction / distance, distance,
            obstacleLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform == target || hitTransform.IsChildOf(target))
                continue;
            return false;
        }
        return true;
    }

    private void PickWanderDestination()
    {
        Vector2 offset = UnityEngine.Random.insideUnitCircle * wanderRadius;
        wanderDestination = homePosition + new Vector3(offset.x,
            UnityEngine.Random.Range(-hoverHeightVariation, hoverHeightVariation), offset.y);
        nextWanderAt = Time.time + UnityEngine.Random.Range(2f, 5f);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion desired = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, facingYawOffset - 90f, 0f);
        body.MoveRotation(Quaternion.Slerp(body.rotation, desired, turnSpeed * Time.fixedDeltaTime));
    }

    private void SteerVelocity(Vector3 desiredVelocity)
    {
        body.AddForce((desiredVelocity - body.linearVelocity) * flightForce, ForceMode.Acceleration);
    }

    private bool IsNearGround()
    {
        float checkDistance = Mathf.Max(0.05f, collisionRadius) + Mathf.Max(0f, groundLungeHeight);
        RaycastHit[] hits = Physics.RaycastAll(body.position, Vector3.down, checkDistance,
            groundLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.normal.y < 0.5f) continue;
            Rigidbody groundBody = hit.collider.attachedRigidbody;
            if (groundBody != null && !groundBody.isKinematic) continue;
            return true;
        }
        return false;
    }

    private bool IsBlocked(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return false;
        float radius = Mathf.Max(0.05f, collisionRadius);
        RaycastHit[] hits = Physics.SphereCastAll(body.position, radius, direction.normalized, 0.3f,
            obstacleLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (chaseTarget != null && (hit.collider.transform == chaseTarget ||
                hit.collider.transform.IsChildOf(chaseTarget))) continue;
            // Let physics handle a supporting floor, including when the target is below the book.
            if (hit.normal.y >= 0.5f) continue;
            return true;
        }
        return false;
    }

    [ContextMenu("Capture Start Pose")]
    public void CaptureStartPose()
    {
        if (!CanCapture()) return;
        RecordChange("Capture mimic start pose");
        upperHalf.start.Capture(upperHalf.target);
        lowerHalf.start.Capture(lowerHalf.target);
        hasStartPose = true;
        SaveChange();
    }

    [ContextMenu("Capture End Pose")]
    public void CaptureEndPose()
    {
        if (!CanCapture()) return;
        RecordChange("Capture mimic end pose");
        upperHalf.end.Capture(upperHalf.target);
        lowerHalf.end.Capture(lowerHalf.target);
        hasEndPose = true;
        SaveChange();
    }

    [ContextMenu("Show Start Pose")]
    public void ShowStartPose()
    {
        if (hasStartPose && HasTargets()) ShowPose(0f);
    }

    [ContextMenu("Show End Pose")]
    public void ShowEndPose()
    {
        if (hasEndPose && HasTargets()) ShowPose(1f);
    }

    private void ShowPose(float progress)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObjects(
                new UnityEngine.Object[] { upperHalf.target, lowerHalf.target }, "Show mimic pose");
#endif
        ApplyPose(progress);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(upperHalf.target);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(lowerHalf.target);
        }
#endif
    }

    private void ApplyPose(float progress)
    {
        ApplyHalf(upperHalf, progress);
        ApplyHalf(lowerHalf, progress);
    }

    private static void ApplyHalf(BookHalf half, float progress)
    {
        half.target.localPosition = Vector3.Lerp(half.start.position, half.end.position, progress);
        half.target.localRotation = Quaternion.Slerp(
            Quaternion.Euler(half.start.rotation), Quaternion.Euler(half.end.rotation), progress);
    }

    private bool HasTargets()
    {
        return upperHalf.target != null && lowerHalf.target != null &&
               upperHalf.target != lowerHalf.target;
    }

    private bool CanCapture()
    {
        if (HasTargets()) return true;
        Debug.LogWarning("Assign two different book halves before capturing a pose.", this);
        return false;
    }

    private void RecordChange(string description)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObject(this, description);
#endif
    }

    private void SaveChange()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
#endif
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(MimicBookTest))]
public class MimicBookTestEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MimicBookTest book = (MimicBookTest)target;
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.LabelField("Pose Tools", UnityEditor.EditorStyles.boldLabel);

        using (new UnityEditor.EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Capture Start Pose"))
                book.CaptureStartPose();
            if (GUILayout.Button("Capture End Pose"))
                book.CaptureEndPose();
            if (GUILayout.Button("Show Start Pose"))
                book.ShowStartPose();
            if (GUILayout.Button("Show End Pose"))
                book.ShowEndPose();
        }

        if (Application.isPlaying)
            UnityEditor.EditorGUILayout.HelpBox("Edit poses outside Play mode so they are saved.", UnityEditor.MessageType.Info);
    }
}
#endif
