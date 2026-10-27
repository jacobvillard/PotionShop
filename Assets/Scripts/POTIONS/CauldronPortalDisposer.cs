using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Destroys potion and item-layer objects dropped into this trigger, while
/// briefly intensifying a portal material. Everything else is ejected instead.
/// Attach this to the cauldron's portal/disposal trigger collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CauldronPortalDisposer : MonoBehaviour
{
    [Header("Accepted Objects")]
    [Tooltip("Only objects on these physics layers are consumed. The defaults are Potion and Items.")]
    [SerializeField] private LayerMask acceptedLayers = (1 << 3) | (1 << 6);

    [Header("Portal Visual")]
    [Tooltip("The renderer using the Portal shader. Its material is copied at runtime, so the material asset is never changed.")]
    [SerializeField] private Renderer portalRenderer;
    [SerializeField] private Material portalMaterial;
    [SerializeField, Min(0f)] private float boostDuration = 0.4f;
    [SerializeField] private string speedProperty = "_Speed";
    [SerializeField] private string strengthProperty = "_Strength";
    [SerializeField] private string emissionProperty = "_Emission";
    [SerializeField] private float speedIncrease = 0.15f;
    [SerializeField] private float strengthIncrease = 0.75f;
    [SerializeField] private float emissionIncrease = 0.75f;
    [Tooltip("Controls the boost over its duration. It rises to full strength halfway through, then returns to normal.")]
    [SerializeField] private AnimationCurve boostCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -3f, 0f));

    [Header("Deposit Feedback")]
    [Tooltip("Played through the project's SFX mixer when an accepted object is consumed.")]
    [SerializeField] private AudioClip depositSfx;
    [SerializeField, Range(0f, 1f)] private float depositSfxVolume = 0.8f;
    [Tooltip("A particle object already placed on the cauldron. It is enabled and restarted for every accepted deposit.")]
    [SerializeField] private GameObject depositParticles;
    [Tooltip("The particle object is disabled after this delay. Set this to match the effect's total duration.")]
    [SerializeField, Min(0f)] private float depositParticleDisableDelay = 2f;

    [Header("Rejected Objects")]
    [Tooltip("Rejected objects are given a moment to drop into the cauldron before being launched out.")]
    [SerializeField, Min(0f)] private float ejectionDelay = 0.1f;
    [Tooltip("The guaranteed upward velocity given to a rejected object. This replaces any downward fall speed.")]
    [SerializeField, Min(0f)] private float ejectionUpwardVelocity = 6f;
    [SerializeField, Min(0f)] private float ejectionOutwardVelocity = 3f;
    [Tooltip("How long the ejection velocity is held, ensuring an object clears the cauldron rim.")]
    [SerializeField, Min(0f)] private float ejectionVelocityHoldDuration = 0.12f;

    private readonly HashSet<EntityId> handledObjectIds = new();
    private Material runtimePortalMaterial;
    private Material originalRendererMaterial;
    private float baseSpeed;
    private float baseStrength;
    private float baseEmission;
    private float boostStartedAt = -1f;
    private Coroutine particleDisableRoutine;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;

        InitialisePortalMaterial();
    }

    private void Update()
    {
        if (runtimePortalMaterial == null || boostStartedAt < 0f)
            return;

        float duration = Mathf.Max(boostDuration, Mathf.Epsilon);
        float progress = Mathf.Clamp01((Time.time - boostStartedAt) / duration);
        float boostAmount = Mathf.Clamp01(boostCurve.Evaluate(progress));
        ApplyPortalBoost(boostAmount);

        if (progress >= 1f)
        {
            RestorePortalValues();
            boostStartedAt = -1f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject target = GetTargetObject(other);
        if (target == null || target == gameObject || target.transform.IsChildOf(transform))
            return;

        EntityId targetId = target.GetEntityId();
        if (!handledObjectIds.Add(targetId))
            return;

        // Staffs are never consumed, even if someone later puts one on an
        // accepted layer. They are ejected like all other rejected objects.
        if (target.GetComponentInChildren<Staff>() != null || !IsOnAcceptedLayer(other, target))
        {
            StartCoroutine(EjectAfterDelay(target, targetId));
            return;
        }

        TriggerPortalBoost();
        PlayDepositFeedback(other.ClosestPoint(transform.position));
        Destroy(target);
    }

    private GameObject GetTargetObject(Collider other)
    {
        if (other == null)
            return null;

        return other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.transform.root.gameObject;
    }

    private bool IsOnAcceptedLayer(Collider other, GameObject target)
    {
        int otherMask = 1 << other.gameObject.layer;
        int targetMask = 1 << target.layer;
        return (acceptedLayers.value & (otherMask | targetMask)) != 0;
    }

    private IEnumerator EjectAfterDelay(GameObject target, EntityId targetId)
    {
        yield return new WaitForSeconds(ejectionDelay);

        if (target != null)
        {
            Rigidbody targetBody = target.GetComponentInChildren<Rigidbody>();
            if (targetBody != null && !targetBody.isKinematic)
            {
                Vector3 outward = targetBody.worldCenterOfMass - transform.position;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : transform.forward;
                Vector3 velocityChange = outward * ejectionOutwardVelocity + Vector3.up * ejectionUpwardVelocity;

                // Set velocity instead of adding force: a fast downward-falling
                // object can otherwise cancel a weak upward impulse and settle
                // inside the cauldron.
                float heldTime = 0f;
                do
                {
                    targetBody.linearVelocity = velocityChange;
                    yield return new WaitForFixedUpdate();
                    heldTime += Time.fixedDeltaTime;
                }
                while (targetBody != null && heldTime < ejectionVelocityHoldDuration);
            }
        }

        handledObjectIds.Remove(targetId);
    }

    private void InitialisePortalMaterial()
    {
        Material source = portalMaterial;
        if (source == null && portalRenderer != null)
            source = portalRenderer.sharedMaterial;

        if (source == null)
        {
            Debug.LogWarning($"{name}: Assign a Portal Renderer or Portal Material to animate the disposal effect.", this);
            return;
        }

        runtimePortalMaterial = new Material(source)
        {
            name = $"{source.name} (Cauldron Portal Instance)"
        };

        if (portalRenderer != null)
        {
            originalRendererMaterial = portalRenderer.sharedMaterial;
            portalRenderer.sharedMaterial = runtimePortalMaterial;
        }

        baseSpeed = GetFloat(speedProperty);
        baseStrength = GetFloat(strengthProperty);
        baseEmission = GetFloat(emissionProperty);
    }

    private void TriggerPortalBoost()
    {
        if (runtimePortalMaterial == null)
            return;

        boostStartedAt = Time.time;
        ApplyPortalBoost(0f);
    }

    private void PlayDepositFeedback(Vector3 position)
    {
        SfxAudio.PlayAt(depositSfx, position, depositSfxVolume);

        if (depositParticles == null)
            return;

        depositParticles.SetActive(true);
        foreach (ParticleSystem particleSystem in depositParticles.GetComponentsInChildren<ParticleSystem>(true))
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }

        if (particleDisableRoutine != null)
            StopCoroutine(particleDisableRoutine);

        particleDisableRoutine = StartCoroutine(DisableDepositParticlesAfterDelay());
    }

    private IEnumerator DisableDepositParticlesAfterDelay()
    {
        yield return new WaitForSeconds(depositParticleDisableDelay);

        if (depositParticles != null)
            depositParticles.SetActive(false);

        particleDisableRoutine = null;
    }

    private float GetFloat(string propertyName)
    {
        return HasProperty(propertyName) ? runtimePortalMaterial.GetFloat(propertyName) : 0f;
    }

    private void SetFloat(string propertyName, float value)
    {
        if (HasProperty(propertyName))
            runtimePortalMaterial.SetFloat(propertyName, value);
    }

    private bool HasProperty(string propertyName)
    {
        return !string.IsNullOrWhiteSpace(propertyName) && runtimePortalMaterial.HasProperty(propertyName);
    }

    private void RestorePortalValues()
    {
        ApplyPortalBoost(0f);
    }

    private void ApplyPortalBoost(float boostAmount)
    {
        SetFloat(speedProperty, baseSpeed + speedIncrease * boostAmount);
        SetFloat(strengthProperty, baseStrength + strengthIncrease * boostAmount);
        SetFloat(emissionProperty, baseEmission + emissionIncrease * boostAmount);
    }

    private void OnDestroy()
    {
        if (portalRenderer != null && originalRendererMaterial != null)
            portalRenderer.sharedMaterial = originalRendererMaterial;

        if (runtimePortalMaterial != null)
            Destroy(runtimePortalMaterial);
    }

    private void OnValidate()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }
}
