using System.Collections;
using UnityEngine;

public class DripDispenser : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private DripJarSlot jarSlot;
    [SerializeField] private Transform outlet;
    [SerializeField] private Transform cauldronCheckPoint;
    [SerializeField] private GameObject liquidStream;
    [SerializeField] private Renderer liquidStreamRenderer;

    [Header("Stream Materials")]
    [SerializeField] private Material milkStreamMaterial;
    [SerializeField] private Material honeyStreamMaterial;

    [Header("Cauldron Check")]
    [SerializeField, Min(0.01f)] private float cauldronCheckRadius = 0.35f;
    [SerializeField, Range(0f, 1f)] private float minimumUprightDot = 0.9f;
    [SerializeField] private LayerMask cauldronLayers = ~0;

    [Header("Dispensing")]
    [SerializeField, Min(0.01f)] private float fillRate = 0.2f;
    [SerializeField] private string streamFillProperty = "_Fill";
    [SerializeField, Min(0f)] private float streamTurnOnDuration = 0.15f;
    [SerializeField, Min(0f)] private float streamTurnOffDuration = 0.2f;

    [Header("SFX")]
    [SerializeField] private AudioClip dispensingLoopSfx;
    [SerializeField, Range(0f, 1f)] private float dispensingLoopVolume = 0.7f;

    private Coroutine dispensingRoutine;
    private MaterialPropertyBlock streamPropertyBlock;
    private int streamFillPropertyId;
    private Material streamMaterialInstance;
    private AudioSource dispensingAudioSource;

    public bool IsDispensing => dispensingRoutine != null;

    private void Awake()
    {
        streamPropertyBlock = new MaterialPropertyBlock();
        streamFillPropertyId = Shader.PropertyToID(streamFillProperty);

        if (outlet == null)
            outlet = transform;

        if (cauldronCheckPoint == null)
            cauldronCheckPoint = outlet;

        if (liquidStream != null)
            liquidStream.SetActive(false);

        SetStreamFill(0f);
        dispensingAudioSource = SfxAudio.CreateLoopSource(
            gameObject, dispensingLoopSfx, dispensingLoopVolume);
    }

    private void OnDestroy()
    {
        jarSlot?.SetRemovalLocked(false);

        if (streamMaterialInstance != null)
            Destroy(streamMaterialInstance);
    }

    private void OnDisable()
    {
        // Coroutines stop when disabled; never leave the inserted jar locked.
        jarSlot?.SetRemovalLocked(false);
    }

    public void Interact()
    {
        Debug.Log("DripDispenser: Interact called.");
        if (IsDispensing || jarSlot == null || !jarSlot.HasLockedJar)
            return;
        
        Debug.Log("DripDispenser: Starting dispensing routine.");

        IngredientJar jar = jarSlot.LockedJar;
        Cauldron cauldron = FindValidCauldron(jar.Ingredient);

        
        if (cauldron == null)
            return;
        
        Debug.Log($"DripDispenser: Found valid cauldron for ingredient {jar.Ingredient}.");

        SetStreamMaterial(jar.Ingredient);
        dispensingRoutine = StartCoroutine(Dispense(jar, cauldron));
    }

    private IEnumerator Dispense(IngredientJar jar, Cauldron cauldron)
    {
        float amountToDispense = cauldron.GetRemainingCapacity(jar.Ingredient);
        float jarAmountToConsume = jar.AmountPerUse;

        if (amountToDispense <= Mathf.Epsilon)
        {
            dispensingRoutine = null;
            yield break;
        }

        cauldron.BeginFilling();
        jarSlot?.SetRemovalLocked(true);

        if (liquidStream != null)
            liquidStream.SetActive(true);

        SfxAudio.RouteToSfxMixer(dispensingAudioSource);
        dispensingAudioSource?.Play();

        yield return AnimateStream(0f, 1f, streamTurnOnDuration);

        float dispensedAmount = 0f;

        while (dispensedAmount < amountToDispense)
        {
            if (jarSlot == null || jarSlot.LockedJar != jar)
                break;

            float amountThisFrame = Mathf.Min(
                fillRate * Time.deltaTime,
                amountToDispense - dispensedAmount
            );

            cauldron.AddLiquid(jar.Ingredient, amountThisFrame);
            jar.Consume(jarAmountToConsume *
                (amountThisFrame / amountToDispense));
            dispensedAmount += amountThisFrame;

            yield return null;
        }

        if (liquidStream != null)
        {
            yield return AnimateStream(1f, 0f, streamTurnOffDuration);
            liquidStream.SetActive(false);
        }

        dispensingAudioSource?.Stop();

        cauldron.FinishFilling();
        jarSlot?.SetRemovalLocked(false);
        dispensingRoutine = null;
    }

    private Cauldron FindValidCauldron(LiquidIngredient ingredient)
    {
        Collider[] hits = Physics.OverlapSphere(
            cauldronCheckPoint.position,
            cauldronCheckRadius,
            cauldronLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            Cauldron cauldron = hit.GetComponentInParent<Cauldron>();

            if (IsValidCauldron(cauldron, ingredient))
                return cauldron;
        }

        return null;
    }

    private bool IsValidCauldron(
        Cauldron cauldron,
        LiquidIngredient ingredient,
        bool allowCurrentlyFilling = false)
    {
        if (cauldron == null)
            return false;

        if (!allowCurrentlyFilling && cauldron.IsBeingFilled)
            return false;

        // A drip station can add its base ingredient only once. Other
        // ingredients in the cauldron do not affect this validation.
        if (cauldron.GetLiquidAmount(ingredient) > Mathf.Epsilon)
            return false;

        if (!cauldron.CanReceiveLiquid(ingredient))
            return false;

        // This cauldron model is authored at -90 degrees on X, so its local
        // forward axis is its physical upright direction in world space.
        if (Vector3.Dot(cauldron.transform.forward, Vector3.up) < minimumUprightDot)
            return false;

        return cauldron.transform.position.y < outlet.position.y;
    }

    private IEnumerator AnimateStream(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetStreamFill(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetStreamFill(Mathf.Lerp(from, to, progress));
            yield return null;
        }

        SetStreamFill(to);
    }

    private void SetStreamFill(float amount)
    {
        if (liquidStreamRenderer == null)
            return;

        liquidStreamRenderer.GetPropertyBlock(streamPropertyBlock);
        streamPropertyBlock.SetFloat(streamFillPropertyId, Mathf.Clamp01(amount));
        liquidStreamRenderer.SetPropertyBlock(streamPropertyBlock);
    }

    private void SetStreamMaterial(LiquidIngredient ingredient)
    {
        if (liquidStreamRenderer == null)
            return;

        Material sourceMaterial = ingredient switch
        {
            LiquidIngredient.Milk => milkStreamMaterial,
            LiquidIngredient.Honey => honeyStreamMaterial,
            _ => null
        };

        if (sourceMaterial == null)
            return;

        if (streamMaterialInstance != null)
            Destroy(streamMaterialInstance);

        // The animated stream gets its own material so its _Fill animation
        // never edits ObjHoney/ObjMilk or another dispenser's material.
        streamMaterialInstance = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name} (Drip Stream Instance)"
        };

        liquidStreamRenderer.sharedMaterial = streamMaterialInstance;
    }
}
