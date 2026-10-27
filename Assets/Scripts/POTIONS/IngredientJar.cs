using UnityEngine;

public class IngredientJar : MonoBehaviour, IInteractable, IHoldable
{
    [SerializeField] private LiquidIngredient ingredient = LiquidIngredient.Honey;
    [SerializeField, Min(0f)] private float remainingAmount = 1f;
    [SerializeField, Min(0f)] private float amountPerUse = 0.5f;
    [SerializeField] private Rigidbody rb;

    [Header("Liquid Visual")]
    [SerializeField] private GameObject liquidObject;
    [SerializeField] private Renderer liquidRenderer;
    [SerializeField, Range(0f, 1f)] private float minimumVisualFill = 0.525f;
    [SerializeField, Range(0f, 1f)] private float maximumVisualFill = 0.585f;
    [Tooltip("Maximum amount per second that the visible liquid level can change.")]
    [SerializeField, Min(0.01f)] private float visualAmountPerSecond = 0.25f;

    private DripJarSlot lockedSlot;
    private static readonly int FillProperty = Shader.PropertyToID("_Fill");

    private float startingAmount;
    private float displayedAmount;
    private Material liquidMaterialInstance;
    private MaterialPropertyBlock liquidPropertyBlock;

    public LiquidIngredient Ingredient => ingredient;
    public float RemainingAmount => remainingAmount;
    public float AmountPerUse => Mathf.Min(amountPerUse, remainingAmount);
    public bool IsHeld { get; private set; }
    public bool IsLocked => lockedSlot != null;
    // Jars are intentionally allowed to lock while held: inserting one into
    // the extractor hands it directly from the player to the station.
    public bool CanLockInSlot => !IsLocked && remainingAmount > Mathf.Epsilon;

    /// <summary>Colour used by effects driven by this jar's liquid.</summary>
    public Color GetLiquidVisualColour()
    {
        if (liquidRenderer == null || liquidRenderer.sharedMaterial == null)
            return Color.white;

        Material material = liquidRenderer.sharedMaterial;
        if (material.HasProperty("_SideColor"))
            return material.GetColor("_SideColor");
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");

        return Color.white;
    }

    private Transform originalParent;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        originalParent = transform.parent;
        startingAmount = Mathf.Max(remainingAmount, Mathf.Epsilon);
        displayedAmount = remainingAmount;
        InitialiseLiquidVisual();
    }

    private void Update()
    {
        if (Mathf.Approximately(displayedAmount, remainingAmount))
            return;

        displayedAmount = Mathf.MoveTowards(
            displayedAmount,
            remainingAmount,
            visualAmountPerSecond * Time.deltaTime);
        UpdateLiquidVisual();
    }

    private void OnDestroy()
    {
        if (liquidMaterialInstance != null)
            Destroy(liquidMaterialInstance);
    }

    public void SetIsHeld(bool isHeld)
    {
        IsHeld = isHeld;
    }

    public bool Consume(float amount)
    {
        if (amount <= 0f || remainingAmount <= 0f)
            return false;

        remainingAmount = Mathf.Max(0f, remainingAmount - amount);
        return true;
    }

    private void InitialiseLiquidVisual()
    {
        if (liquidObject == null)
        {
            Transform liquidTransform = transform.Find("Liquid");
            liquidObject = liquidTransform != null ? liquidTransform.gameObject : null;
        }

        if (liquidRenderer == null && liquidObject != null)
            liquidRenderer = liquidObject.GetComponent<Renderer>();

        if (liquidRenderer == null || liquidRenderer.sharedMaterial == null)
            return;

        // Each jar owns a copy so its draining level cannot modify the
        // shared Honey/Milk material asset or another jar.
        liquidMaterialInstance = new Material(liquidRenderer.sharedMaterial)
        {
            name = $"{liquidRenderer.sharedMaterial.name} (Jar Instance)"
        };

        liquidRenderer.sharedMaterial = liquidMaterialInstance;
        liquidPropertyBlock = new MaterialPropertyBlock();
        UpdateLiquidVisual();
    }

    private void UpdateLiquidVisual()
    {
        if (liquidRenderer == null || liquidPropertyBlock == null)
            return;

        float normalisedAmount = Mathf.Clamp01(displayedAmount / startingAmount);

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
        liquidRenderer.SetPropertyBlock(liquidPropertyBlock);

        // The jar owns this material instance. Set the value there as well as
        // on the renderer property block so shaders that do not honour the
        // property block still show the level changing during a drip pour.
        if (liquidMaterialInstance != null && liquidMaterialInstance.HasProperty(FillProperty))
            liquidMaterialInstance.SetFloat(
                FillProperty,
                Mathf.Lerp(minimumVisualFill, maximumVisualFill, normalisedAmount));
    }

    public void LockToSlot(DripJarSlot slot, Transform lockPoint)
    {
        if (!CanLockInSlot)
            return;

        lockedSlot = slot;

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

    public void UnlockFromSlot()
    {
        if (lockedSlot == null)
            return;

        lockedSlot = null;
        transform.SetParent(originalParent);

        if (rb != null)
            rb.isKinematic = false;
    }

    public void Interact()
    {
        if (lockedSlot != null)
            lockedSlot.TryRemoveJar();
    }
}
