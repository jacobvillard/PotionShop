using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implement on any pickup that can be consumed by a cauldron deposit trigger.
/// </summary>
public interface ICauldronIngredient
{
    string CauldronIngredientId { get; }
    string CauldronIngredientDisplayName { get; }
}

public class Cauldron : MonoBehaviour, ISinkable, IInteractable, IStaffable
{
    [System.Serializable]
    public class LiquidContent
    {
        public LiquidIngredient ingredient;
        public float amount;
    }

    [System.Serializable]
    public class AdditionalIngredientContent
    {
        public string ingredientId;
        public string displayName;
        public int amount;
    }

    [Header("Capacity")]
    [SerializeField] private float maxLiquidAmount = 1f;

    [Header("Current Contents")]
    [SerializeField] private List<LiquidContent> liquidContents = new();
    [SerializeField] private List<AdditionalIngredientContent> additionalIngredients = new();

    [Header("Physics")]
    [SerializeField] private Rigidbody rb;

    [Header("Liquid Visual")]
    [SerializeField] private GameObject liquidObject;
    [SerializeField] private Renderer liquidRenderer;
    [SerializeField] private Material defaultLiquidMaterial;
    [SerializeField] private Material waterLiquidMaterial;
    [SerializeField] private Material milkLiquidMaterial;
    [SerializeField] private Material honeyLiquidMaterial;
    [SerializeField, Range(0f, 1f)] private float minimumVisualFill = 0.525f;
    [SerializeField, Range(0f, 1f)] private float maximumVisualFill = 0.585f;

    [Header("Boiling")]
    [SerializeField] private GameObject boilingTimerPrefab;
    [SerializeField] private bool preserveBoilingTimerPrefabScale = true;
    [SerializeField, Min(0f)] private float boilingTimerHeight = 0.35f;

    [Header("Boiling Effects")]
    [SerializeField] private GameObject bubblesObject;
    [SerializeField] private Renderer bubblesRenderer;
    [SerializeField] private ParticleSystem smokeParticles;

    [Header("Potion Conversion")]
    [SerializeField] private PotionRecipeBook recipeBook;

    private static readonly int FillProperty = Shader.PropertyToID("_Fill");
    private static readonly int SideColorProperty = Shader.PropertyToID("_SideColor");
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private Material liquidMaterialInstance;
    private Material bubblesMaterialInstance;
    private MaterialPropertyBlock liquidPropertyBlock;
    private GameObject boilingTimerInstance;

    public bool IsLockedInSink { get; private set; }

    public bool IsBeingFilled { get; private set; }
    public bool IsBoiling { get; private set; }
    public bool IsBoiled { get; private set; }
    public IReadOnlyList<AdditionalIngredientContent> AdditionalIngredients => additionalIngredients;

    public float CurrentLiquidAmount
    {
        get
        {
            float total = 0f;

            foreach (LiquidContent content in liquidContents)
            {
                total += content.amount;
            }

            return total;
        }
    }

    // Capacity applies independently to every ingredient, not to the combined
    // contents of the cauldron.
    public bool IsFull => liquidContents.Exists(content =>
        content != null && content.amount >= maxLiquidAmount);

    // A cauldron can only be placed in the sink before it has received liquid.
    public bool IsEmpty => !liquidContents.Exists(content =>
        content != null && content.amount > Mathf.Epsilon);

    public bool CanBeRemovedFromSink => !IsBeingFilled;
    public bool CanLockInSink => IsEmpty && !IsHeld;
    public bool IsHeld { get; private set; }

    private Transform originalParent;
    private Sink lockedSink;
    private bool wasKinematicBeforeFilling;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        originalParent = transform.parent;
        if (recipeBook == null)
            recipeBook = Resources.Load<PotionRecipeBook>("PotionRecipeBook");
        InitialiseLiquidVisual();
        InitialiseBoilingTimer();
        InitialiseBoilingEffects();
    }

    private void OnDestroy()
    {
        if (liquidMaterialInstance != null)
            Destroy(liquidMaterialInstance);

        if (bubblesMaterialInstance != null)
            Destroy(bubblesMaterialInstance);

        if (boilingTimerInstance != null && preserveBoilingTimerPrefabScale)
            Destroy(boilingTimerInstance);
    }

    private void LateUpdate()
    {
        if (boilingTimerInstance != null && boilingTimerInstance.activeSelf)
        {
            boilingTimerInstance.transform.SetPositionAndRotation(
                transform.position + Vector3.up * boilingTimerHeight,
                Quaternion.identity);
        }
    }

    // --------------------------------------------------
    // LIQUID
    // --------------------------------------------------

    public bool CanReceiveLiquid(LiquidIngredient liquid)
    {
        return GetLiquidAmount(liquid) < maxLiquidAmount;
    }

    public void AddLiquid(LiquidIngredient liquid, float amount)
    {
        if (amount <= 0f)
            return;

        if (!CanReceiveLiquid(liquid))
            return;

        bool wasEmpty = IsEmpty;

        float remainingCapacity =
            maxLiquidAmount - GetLiquidAmount(liquid);

        float amountToAdd = Mathf.Min(amount, remainingCapacity);

        LiquidContent existing = liquidContents.Find(
            x => x.ingredient == liquid
        );

        if (existing != null)
        {
            existing.amount += amountToAdd;
        }
        else
        {
            liquidContents.Add(new LiquidContent
            {
                ingredient = liquid,
                amount = amountToAdd
            });
        }

        // The first ingredient determines the visible base material. Once the
        // cauldron has contents, do not change that material mid-mixture.
        if (wasEmpty)
            SetLiquidMaterial(GetMaterialForIngredient(liquid));

        Debug.Log(
            $"Added {amountToAdd} {liquid}. " +
            $"Cauldron now contains {CurrentLiquidAmount}/{maxLiquidAmount}"
        );

        UpdateLiquidVisual();
    }

    public float GetLiquidAmount(LiquidIngredient ingredient)
    {
        LiquidContent content = liquidContents.Find(
            x => x.ingredient == ingredient
        );

        return content?.amount ?? 0f;
    }

    public float GetRemainingCapacity(LiquidIngredient ingredient)
    {
        return Mathf.Max(0f, maxLiquidAmount - GetLiquidAmount(ingredient));
    }

    public bool TryAddPetal(Petal petal)
    {
        return petal != null && TryAddIngredient(petal);
    }

    public bool TryAddIngredient(ICauldronIngredient ingredient)
    {
        if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.CauldronIngredientId))
            return false;

        AdditionalIngredientContent existing = additionalIngredients.Find(
            content => content != null && content.ingredientId == ingredient.CauldronIngredientId);

        if (existing != null)
        {
            existing.amount++;
        }
        else
        {
            additionalIngredients.Add(new AdditionalIngredientContent
            {
                ingredientId = ingredient.CauldronIngredientId,
                displayName = ingredient.CauldronIngredientDisplayName,
                amount = 1
            });
        }

        return true;
    }

    public int GetAdditionalIngredientAmount(string ingredientId)
    {
        AdditionalIngredientContent content = additionalIngredients.Find(
            entry => entry != null && entry.ingredientId == ingredientId);
        return content?.amount ?? 0;
    }

    public bool BeginBoiling()
    {
        if (IsBoiling || IsBoiled || IsBeingFilled || CurrentLiquidAmount <= Mathf.Epsilon)
            return false;

        IsBoiling = true;

        if (liquidObject != null)
            liquidObject.SetActive(true);

        SetBoilingEffectsActive(true);
        SetBoilingTimerVisible(true);
        return true;
    }

    public void StopBoiling()
    {
        if (!IsBoiling)
            return;

        IsBoiling = false;
        SetBoilingEffectsActive(false);
        SetBoilingTimerVisible(false);
    }

    public void FinishBoiling()
    {
        if (!IsBoiling)
            return;

        IsBoiling = false;
        IsBoiled = true;
        SetBoilingEffectsActive(false);
        SetBoilingTimerVisible(false);
    }

    public Color GetLiquidVisualColour()
    {
        Color combinedColour = Color.black;
        float totalAmount = 0f;

        foreach (LiquidContent content in liquidContents)
        {
            if (content == null || content.amount <= Mathf.Epsilon)
                continue;

            Material material = GetMaterialForIngredient(content.ingredient);
            if (material == null)
                continue;

            combinedColour += GetMaterialLiquidColour(material) * content.amount;
            totalAmount += content.amount;
        }

        return totalAmount > Mathf.Epsilon
            ? combinedColour / totalAmount
            : Color.white;
    }

    private void InitialiseLiquidVisual()
    {
        if (liquidObject == null && liquidRenderer != null)
            liquidObject = liquidRenderer.gameObject;

        if (liquidRenderer == null && liquidObject != null)
            liquidRenderer = liquidObject.GetComponent<Renderer>();

        if (liquidRenderer == null)
            return;

        Material sourceMaterial = defaultLiquidMaterial != null
            ? defaultLiquidMaterial
            : liquidRenderer.sharedMaterial;

        SetLiquidMaterial(sourceMaterial);
        liquidPropertyBlock = new MaterialPropertyBlock();

        UpdateLiquidVisual();
    }

    private void InitialiseBoilingTimer()
    {
        if (boilingTimerPrefab == null)
            return;

        boilingTimerInstance = preserveBoilingTimerPrefabScale
            ? Instantiate(boilingTimerPrefab)
            : Instantiate(boilingTimerPrefab, transform);
        boilingTimerInstance.SetActive(false);
    }

    private void InitialiseBoilingEffects()
    {
        if (bubblesObject != null)
            bubblesObject.SetActive(false);

        if (smokeParticles != null)
            smokeParticles.gameObject.SetActive(false);

        if (bubblesRenderer != null && bubblesRenderer.sharedMaterial != null)
        {
            bubblesMaterialInstance = new Material(bubblesRenderer.sharedMaterial)
            {
                name = $"{bubblesRenderer.sharedMaterial.name} (Cauldron Bubbles Instance)"
            };
            bubblesRenderer.sharedMaterial = bubblesMaterialInstance;
        }
    }

    private Material GetMaterialForIngredient(LiquidIngredient ingredient)
    {
        return ingredient switch
        {
            LiquidIngredient.Water => waterLiquidMaterial,
            LiquidIngredient.Milk => milkLiquidMaterial,
            LiquidIngredient.Honey => honeyLiquidMaterial,
            _ => defaultLiquidMaterial
        } ?? defaultLiquidMaterial;
    }

    private void SetLiquidMaterial(Material sourceMaterial)
    {
        if (liquidRenderer == null || sourceMaterial == null)
            return;

        if (liquidMaterialInstance != null)
            Destroy(liquidMaterialInstance);

        // Every cauldron owns a runtime copy, so changing its fill can never
        // modify the Water material asset or another cauldron's liquid.
        liquidMaterialInstance = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name} (Cauldron Instance)"
        };

        liquidRenderer.sharedMaterial = liquidMaterialInstance;
    }

    private void UpdateLiquidVisual()
    {
        if (liquidRenderer == null || liquidPropertyBlock == null)
            return;

        float normalisedAmount = maxLiquidAmount > 0f
            ? Mathf.Clamp01(CurrentLiquidAmount / maxLiquidAmount)
            : 0f;

        // The liquid mesh has a narrow usable shader-fill range. Map the
        // gameplay amount linearly across the calibrated endpoints.
        if (liquidObject != null)
            liquidObject.SetActive(normalisedAmount > Mathf.Epsilon);

        liquidRenderer.GetPropertyBlock(liquidPropertyBlock);
        liquidPropertyBlock.SetFloat(
            FillProperty,
            Mathf.Lerp(
                minimumVisualFill,
                maximumVisualFill,
                normalisedAmount
            )
        );
        SetMixedLiquidColors(liquidPropertyBlock);
        liquidRenderer.SetPropertyBlock(liquidPropertyBlock);
    }

    private void SetMixedLiquidColors(MaterialPropertyBlock propertyBlock)
    {
        if (liquidContents.Count == 0)
            return;

        Color sideColor = Color.black;
        float totalAmount = 0f;

        foreach (LiquidContent content in liquidContents)
        {
            if (content == null || content.amount <= Mathf.Epsilon)
                continue;

            Material material = GetMaterialForIngredient(content.ingredient);

            if (material == null)
                continue;

            Color ingredientSideColor = GetMaterialLiquidColour(material);
            sideColor += ingredientSideColor * content.amount;
            totalAmount += content.amount;
        }

        if (totalAmount <= Mathf.Epsilon)
            return;

        // Always set SideColor, including a single liquid. This clears any
        // previous mixed-colour property block before the next fill.
        propertyBlock.SetColor(SideColorProperty, sideColor / totalAmount);
    }

    private static Color GetMaterialLiquidColour(Material material)
    {
        if (material.HasProperty(SideColorProperty))
            return material.GetColor(SideColorProperty);

        if (material.HasProperty(BaseColorProperty))
            return material.GetColor(BaseColorProperty);

        return material.HasProperty(ColorProperty)
            ? material.GetColor(ColorProperty)
            : Color.white;
    }

    private void SetBoilingTimerVisible(bool visible)
    {
        if (boilingTimerInstance != null)
            boilingTimerInstance.SetActive(visible);
    }

    private void SetBoilingEffectsActive(bool active)
    {
        if (bubblesObject != null)
            bubblesObject.SetActive(active);

        if (active)
        {
            Color liquidColour = GetLiquidVisualColour();
            SetBubbleColour(liquidColour);

            if (smokeParticles != null)
            {
                // Assign before enabling: a Play-On-Awake system otherwise
                // emits its first particles using the old inspector colour.
                ParticleSystem.MainModule main = smokeParticles.main;
                main.startColor = liquidColour;
                smokeParticles.gameObject.SetActive(true);
                smokeParticles.Clear(true);
                smokeParticles.Play(true);
            }
        }
        else if (smokeParticles != null)
        {
            smokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smokeParticles.gameObject.SetActive(false);
        }
    }

    private void SetBubbleColour(Color liquidColour)
    {
        if (bubblesMaterialInstance == null)
            return;

        if (bubblesMaterialInstance.HasProperty(BaseColorProperty))
            bubblesMaterialInstance.SetColor(BaseColorProperty, liquidColour);
        if (bubblesMaterialInstance.HasProperty(ColorProperty))
            bubblesMaterialInstance.SetColor(ColorProperty, liquidColour);
        if (bubblesMaterialInstance.HasProperty(SideColorProperty))
            bubblesMaterialInstance.SetColor(SideColorProperty, liquidColour);
    }

    // --------------------------------------------------
    // FILL STATE
    // --------------------------------------------------

    public void BeginFilling()
    {
        if (IsBeingFilled)
            return;

        IsBeingFilled = true;

        // Dispensing stations may use a cauldron that is not snapped to a
        // sink. Keep it physically still until the measured pour completes.
        if (rb != null)
        {
            wasKinematicBeforeFilling = rb.isKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    public void FinishFilling()
    {
        if (!IsBeingFilled)
            return;

        IsBeingFilled = false;

        if (rb != null)
            rb.isKinematic = wasKinematicBeforeFilling;
    }

    // --------------------------------------------------
    // SINK
    // --------------------------------------------------

    public void SetIsHeld(bool isHeld)
    {
        IsHeld = isHeld;
    }

    public void SetSink(Sink sink)
    {
        lockedSink = sink;
    }

    public void LockToSink(Transform lockPoint)
    {
        if (IsLockedInSink)
            return;

        IsLockedInSink = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        transform.SetParent(lockPoint);

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void UnlockFromSink()
    {
        if (!IsLockedInSink)
            return;

        if (!CanBeRemovedFromSink)
            return;

        IsLockedInSink = false;

        lockedSink = null;

        transform.SetParent(originalParent);

        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }


    public void Interact()
    {
        // The sink owns the lock state. Asking it to remove this cauldron clears
        // its cached locked object as well as releasing this Rigidbody.
        if (lockedSink != null && ReferenceEquals(lockedSink.LockedObject, this))
        {
            lockedSink.TryRemoveObject();
            return;
        }

        UnlockFromSink();
    }

    public void GetStaffedBiatch()
    {
        if (recipeBook == null || !recipeBook.TryGetMatchingRecipe(this, out PotionRecipe recipe))
        {
            Debug.Log($"[{name}] Staff blast found no exact potion recipe match.", this);
            return;
        }

        Transform currentParent = transform.parent;
        Instantiate(recipe.potionPrefab, transform.position, transform.rotation, currentParent);
        Destroy(gameObject);
    }
}
