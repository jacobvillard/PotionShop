using System.Collections;
using UnityEngine;

/// <summary>
/// Turns the liquid in a locked DripJarSlot into one unique cauldron essence.
/// Assign this beside the drip extractor, an EssenceCauldronSlot, the effects,
/// target marker, and an EssenceNeedleController.
/// </summary>
public class EssenceExtractor : MonoBehaviour
{
    [SerializeField] private DripJarSlot sourceJarSlot;
    [SerializeField] private EssenceCauldronSlot cauldronSlot;
    [SerializeField] private EssenceNeedleController needle;
    [SerializeField] private ParticleSystem[] essenceParticles;
    [SerializeField] private Transform passCriteriaObject;
    [Tooltip("Global axis used by the pass-criteria marker.")]
    [SerializeField] private EssenceRotationAxis passCriteriaRotationAxis = EssenceRotationAxis.Y;
    [Tooltip("Choose Local when you want only one local Inspector axis to change.")]
    [SerializeField] private EssenceRotationSpace passCriteriaRotationSpace = EssenceRotationSpace.Local;
    [SerializeField] private bool logTargetDiagnostics = true;
    [Tooltip("Random full-needle global-Y target range.")]
    [SerializeField] private Vector2 targetRotationRange = new Vector2(0f, 360f);
    [Header("Pass-Criteria Visual")]
    [SerializeField] private Renderer passCriteriaRenderer;
    [SerializeField] private Material passCriteriaInactiveMaterial;
    [SerializeField] private Material passCriteriaReadyMaterial;
    [SerializeField, Min(0f)] private float extractionDuration = 2f;
    [Tooltip("0.2 means the extraction effect runs at one fifth of its authored speed.")]
    [SerializeField, Range(0.05f, 1f)] private float extractionSpeed = 0.2f;
    [SerializeField, Min(0f)] private float amountConsumedPerExtraction = 0.5f;

    private IngredientJar observedJar;
    private LiquidIngredient pendingLiquid;
    private bool hasPendingEssence;
    private bool isArmed;
    private bool extracting;
    private Cauldron lastExtractedCauldron;
    private int observedJarSlotVersion = -1;
    private const float AmountComparisonEpsilon = 0.0001f;

    private void Awake()
    {
        if (passCriteriaRenderer == null && passCriteriaObject != null)
            passCriteriaRenderer = passCriteriaObject.GetComponentInChildren<Renderer>();
        if (needle != null && passCriteriaRenderer != null &&
            passCriteriaRenderer == needle.IndicatorRenderer)
            needle.SetIndicatorMaterialUpdatesEnabled(false);
        StopParticles();
        UpdatePassCriteriaMaterial();
    }

    private void Update()
    {
        DetectNewJar();

        UpdateArmedState();

        if (isArmed && !extracting && needle != null && needle.IsAligned)
            TryBeginExtraction();

        UpdatePassCriteriaMaterial();
    }

    private void DetectNewJar()
    {
        IngredientJar currentJar = sourceJarSlot != null ? sourceJarSlot.LockedJar : null;
        int currentSlotVersion = sourceJarSlot != null ? sourceJarSlot.ChangeVersion : -1;
        if (currentJar == observedJar && currentSlotVersion == observedJarSlotVersion)
            return;

        observedJar = currentJar;
        observedJarSlotVersion = currentSlotVersion;
        hasPendingEssence = false;
        isArmed = false;
        lastExtractedCauldron = null;
        if (needle != null)
            needle.ClearTarget();

        if (currentJar == null || !IsExtractable(currentJar.Ingredient))
            return;

        pendingLiquid = currentJar.Ingredient;
        hasPendingEssence = true;
        ArmTargetForCurrentJar();
    }

    private void UpdateArmedState()
    {
        Cauldron currentCauldron = cauldronSlot != null ? cauldronSlot.LockedCauldron : null;
        bool canUseCurrentCauldron = currentCauldron == null ||
            currentCauldron != lastExtractedCauldron;

        if (hasPendingEssence && observedJar != null && !isArmed && canUseCurrentCauldron)
            ArmTargetForCurrentJar();
    }

    private void ArmTargetForCurrentJar()
    {
        if (observedJar == null || !hasPendingEssence)
            return;

        isArmed = true;
        SetParticleColour(observedJar.GetLiquidVisualColour());
        float requestedTargetAngle = Random.Range(targetRotationRange.x, targetRotationRange.y);
        float targetAngle = needle != null
            ? needle.SetTargetAngle(requestedTargetAngle)
            : requestedTargetAngle;
        SetAngle(passCriteriaObject, passCriteriaRotationAxis, passCriteriaRotationSpace, targetAngle);

        // Pass the angle actually applied to the marker. This keeps the
        // success test exact even when the marker lives in a rotated hierarchy.
        float appliedTargetAngle = GetAngle(
            passCriteriaObject,
            passCriteriaRotationAxis,
            passCriteriaRotationSpace);
        if (logTargetDiagnostics)
        {
            Debug.Log(
                $"[EssenceExtractor] Target: requested={requestedTargetAngle:F2}, needle={targetAngle:F2}, marker={appliedTargetAngle:F2}, " +
                $"axis={passCriteriaRotationAxis}, space={passCriteriaRotationSpace}, " +
                $"local={passCriteriaObject?.localEulerAngles}, world={passCriteriaObject?.eulerAngles}",
                this);
        }
    }

    private void TryBeginExtraction()
    {
        Cauldron cauldron = cauldronSlot != null ? cauldronSlot.LockedCauldron : null;
        if (cauldron == null || cauldron.IsBeingFilled || observedJar == null ||
            observedJar.RemainingAmount + AmountComparisonEpsilon < amountConsumedPerExtraction ||
            cauldron.GetAdditionalIngredientAmount(EssenceId(pendingLiquid)) > 0)
            return;

        StartCoroutine(Extract(cauldron, observedJar, pendingLiquid));
    }

    private IEnumerator Extract(Cauldron cauldron, IngredientJar jar, LiquidIngredient liquid)
    {
        extracting = true;
        // This uses the existing filling lock so the player cannot remove the
        // cauldron while an irreversible extraction is under way.
        cauldron.BeginFilling();
        sourceJarSlot?.SetRemovalLocked(true);
        PlayParticles();

        float duration = Mathf.Max(
            extractionDuration / Mathf.Max(extractionSpeed, 0.05f),
            Mathf.Epsilon);
        float elapsed = 0f;
        float consumedAmount = 0f;
        while (elapsed < duration && jar != null)
        {
            float frameDuration = Mathf.Min(Time.deltaTime, duration - elapsed);
            float amountThisFrame = amountConsumedPerExtraction * (frameDuration / duration);
            jar.Consume(amountThisFrame);
            consumedAmount += amountThisFrame;
            elapsed += frameDuration;
            yield return null;
        }

        // Account for frame rounding so a successful extraction always uses
        // precisely the configured half-jar amount.
        if (jar != null && consumedAmount < amountConsumedPerExtraction)
            jar.Consume(amountConsumedPerExtraction - consumedAmount);

        StopParticles();

        if (cauldron != null && jar != null && jar == observedJar)
            cauldron.TryAddEssence(liquid);

        if (cauldron != null)
            cauldron.FinishFilling();
        sourceJarSlot?.SetRemovalLocked(false);

        lastExtractedCauldron = cauldron;
        hasPendingEssence = jar != null &&
            jar.RemainingAmount + AmountComparisonEpsilon >= amountConsumedPerExtraction;
        isArmed = false;
        extracting = false;
        needle?.ClearTarget();
    }

    private static bool IsExtractable(LiquidIngredient liquid) =>
        liquid == LiquidIngredient.Honey || liquid == LiquidIngredient.Milk;

    private static string EssenceId(LiquidIngredient liquid) =>
        $"essence_{liquid.ToString().ToLowerInvariant()}";

    private void SetParticleColour(Color colour)
    {
        foreach (ParticleSystem particles in essenceParticles)
        {
            if (particles == null)
                continue;
            ParticleSystem.MainModule main = particles.main;
            main.startColor = colour;
        }
    }

    private void PlayParticles()
    {
        foreach (ParticleSystem particles in essenceParticles)
            if (particles != null) particles.Play(true);
    }

    private void StopParticles()
    {
        foreach (ParticleSystem particles in essenceParticles)
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdatePassCriteriaMaterial()
    {
        if (passCriteriaRenderer == null)
            return;

        bool aligned = needle != null && needle.IsAligned;
        Material material = aligned
            ? passCriteriaReadyMaterial ?? needle.ReadyMaterial
            : passCriteriaInactiveMaterial ?? (needle != null ? needle.InactiveMaterial : null);
        if (material != null)
            passCriteriaRenderer.sharedMaterial = material;
    }

    private static void SetAngle(Transform target, EssenceRotationAxis axis, EssenceRotationSpace space, float angle)
    {
        if (target == null)
            return;

        Vector3 euler = space == EssenceRotationSpace.Local ? target.localEulerAngles : target.eulerAngles;
        switch (axis)
        {
            case EssenceRotationAxis.X: euler.x = angle; break;
            case EssenceRotationAxis.Y: euler.y = angle; break;
            default: euler.z = angle; break;
        }
        if (space == EssenceRotationSpace.Local)
            target.localEulerAngles = euler;
        else
            target.eulerAngles = euler;
    }

    private static float GetAngle(Transform target, EssenceRotationAxis axis, EssenceRotationSpace space)
    {
        if (target == null)
            return 0f;

        Vector3 euler = space == EssenceRotationSpace.Local
            ? target.localEulerAngles
            : target.eulerAngles;

        return axis switch
        {
            EssenceRotationAxis.X => euler.x,
            EssenceRotationAxis.Y => euler.y,
            _ => euler.z
        };
    }
}
