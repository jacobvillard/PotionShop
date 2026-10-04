using System;
using System.Collections.Generic;
using UnityEngine;

public enum PetalState
{
    Fresh,
    Dried,
    Cooked
}

public enum PetalType
{
    WhiteDaisy,
    PinkDaisy,
    Rose
}

/// <summary>
/// Stores a harvested petal's processing state. Add this to every petal prefab.
/// </summary>
public class Petal : MonoBehaviour, ICauldronIngredient
{
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

    private struct MaterialColour
    {
        public Material Material;
        public string PropertyName;
        public Color OriginalColour;
    }

    [Header("Identity")]
    [SerializeField] private PetalType petalType = PetalType.WhiteDaisy;
    [SerializeField] private PetalState state = PetalState.Fresh;

    [SerializeField, Min(0f)] private float dryingProgress;

    [Header("Drying Appearance")]
    [Tooltip("The HSV Value brightness used once the petal is completely dried.")]
    [SerializeField, Range(0f, 1f)] private float driedMaterialValue = 0.39f;

    [Header("Processing Timer")]
    [Tooltip("Optional looping hourglass prefab, shown above this petal while it is drying or cooking.")]
    [SerializeField] private GameObject processingTimerPrefab;
    [Tooltip("Keeps the timer at its prefab scale instead of inheriting this petal's scale.")]
    [SerializeField] private bool preserveTimerPrefabScale = true;
    [SerializeField, Min(0f)] private float timerHeight = 0.25f;

    private readonly HashSet<int> activeDryingSources = new HashSet<int>();
    private readonly List<MaterialColour> materialColours = new List<MaterialColour>();
    private float dryingDuration;
    private bool isCooking;
    private GameObject processingTimerInstance;

    public PetalType Type => petalType;
    public PetalState State => state;
    public string CauldronIngredientId => GetCauldronIngredientId(petalType, state);
    public string CauldronIngredientDisplayName =>
        $"{GetPetalTypeName(petalType)} {state} Petal";

    public static string GetCauldronIngredientId(PetalType type, PetalState state)
    {
        return $"petal:{type}:{state}";
    }
    public float DryingProgress => dryingProgress;
    public bool IsDrying => activeDryingSources.Count > 0 && state == PetalState.Fresh;
    public bool IsCooking => isCooking;
    public event Action<PetalState> StateChanged;

    private void Awake()
    {
        CacheMaterialInstances();
        ApplyDryingAppearance(state == PetalState.Dried ? 1f : 0f);

        if (processingTimerPrefab != null)
        {
            processingTimerInstance = preserveTimerPrefabScale
                ? Instantiate(processingTimerPrefab)
                : Instantiate(processingTimerPrefab, transform);
            processingTimerInstance.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsDrying)
        {
            return;
        }

        dryingProgress += Time.deltaTime;
        ApplyDryingAppearance(Mathf.Clamp01(dryingProgress / dryingDuration));
        if (dryingProgress >= dryingDuration)
        {
            dryingProgress = dryingDuration;
            activeDryingSources.Clear();
            SetState(PetalState.Dried);
        }
    }

    private void LateUpdate()
    {
        if (processingTimerInstance != null && processingTimerInstance.activeSelf)
        {
            processingTimerInstance.transform.SetPositionAndRotation(
                transform.position + Vector3.up * timerHeight,
                Quaternion.identity);
        }
    }

    /// <summary>Begins or resumes drying. Multiple drying boxes can safely overlap.</summary>
    [Obsolete("Obsolete")]
    public void BeginDrying(UnityEngine.Object dryingSource, float duration)
    {
        if (state != PetalState.Fresh || dryingSource == null)
        {
            return;
        }

        dryingDuration = Mathf.Max(0f, duration);
        activeDryingSources.Add(dryingSource.GetInstanceID());

        if (dryingDuration <= 0f)
        {
            dryingProgress = 0f;
            activeDryingSources.Clear();
            ApplyDryingAppearance(1f);
            SetState(PetalState.Dried);
        }

        UpdateProcessingTimer();
    }

    /// <summary>Pauses drying from this source without losing any accumulated time.</summary>
    [Obsolete("Obsolete")]
    public void StopDrying(UnityEngine.Object dryingSource)
    {
        if (dryingSource != null)
        {
            activeDryingSources.Remove(dryingSource.GetInstanceID());
        }

        UpdateProcessingTimer();
    }

    /// <summary>Call this from a future cooking station to show the same processing timer.</summary>
    public void BeginCooking()
    {
        isCooking = true;
        UpdateProcessingTimer();
    }

    /// <summary>Stops a cooking process without changing the petal state.</summary>
    public void StopCooking()
    {
        isCooking = false;
        UpdateProcessingTimer();
    }

    public void SetCooked()
    {
        if (state == PetalState.Cooked)
        {
            return;
        }

        activeDryingSources.Clear();
        isCooking = false;
        SetState(PetalState.Cooked);
    }

    private void SetState(PetalState newState)
    {
        if (state == newState)
        {
            return;
        }

        state = newState;
        UpdateProcessingTimer();
        StateChanged?.Invoke(state);
    }

    private void CacheMaterialInstances()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in renderer.materials)
            {
                string propertyName = material.HasProperty(BaseColorProperty)
                    ? BaseColorProperty
                    : material.HasProperty(ColorProperty) ? ColorProperty : null;

                if (propertyName != null)
                {
                    materialColours.Add(new MaterialColour
                    {
                        Material = material,
                        PropertyName = propertyName,
                        OriginalColour = material.GetColor(propertyName)
                    });
                }
            }
        }
    }

    private void ApplyDryingAppearance(float dryingAmount)
    {
        foreach (MaterialColour materialColour in materialColours)
        {
            Color.RGBToHSV(materialColour.OriginalColour, out float hue, out float saturation, out float value);
            Color darkenedColour = Color.HSVToRGB(
                hue,
                saturation,
                Mathf.Lerp(value, driedMaterialValue, dryingAmount));
            darkenedColour.a = materialColour.OriginalColour.a;
            materialColour.Material.SetColor(materialColour.PropertyName, darkenedColour);
        }
    }

    private void UpdateProcessingTimer()
    {
        if (processingTimerInstance != null)
        {
            processingTimerInstance.SetActive(IsDrying || isCooking);
        }
    }

    private static string GetPetalTypeName(PetalType type)
    {
        return type switch
        {
            PetalType.WhiteDaisy => "White Daisy",
            PetalType.PinkDaisy => "Pink Daisy",
            PetalType.Rose => "Rose",
            _ => type.ToString()
        };
    }

    private void OnDestroy()
    {
        if (processingTimerInstance != null && preserveTimerPrefabScale)
        {
            Destroy(processingTimerInstance);
        }
    }
}
