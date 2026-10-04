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

    private DripJarSlot lockedSlot;
    private static readonly int FillProperty = Shader.PropertyToID("_Fill");

    private float startingAmount;
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

    private Transform originalParent;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        originalParent = transform.parent;
        startingAmount = Mathf.Max(remainingAmount, Mathf.Epsilon);
        InitialiseLiquidVisual();
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
        UpdateLiquidVisual();
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

        float normalisedAmount = Mathf.Clamp01(remainingAmount / startingAmount);

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
