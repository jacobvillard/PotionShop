using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// A mouse-controlled gauge. Its limited local-Z handle range is mapped onto a
/// separate full-rotation needle. Green means the full needle is at the
/// extractor's randomly assigned global-Y target.
/// </summary>
public enum EssenceRotationAxis
{
    X,
    Y,
    Z
}

public enum EssenceRotationSpace
{
    Local,
    World
}

public class EssenceNeedleController : MonoBehaviour, IInteractable
{
    [Header("Dial Transforms")]
    [SerializeField] private Transform handle;
    [FormerlySerializedAs("needle")]
    [SerializeField] private Transform fullRotationNeedle;

    [Header("Handle Input")]
    [Tooltip("Local axis used by the limited-pivot handle.")]
    [SerializeField] private EssenceRotationAxis handleRotationAxis = EssenceRotationAxis.Z;
    [Tooltip("The handle's authored minimum and maximum angles on its selected local axis.")]
    [FormerlySerializedAs("handleGlobalXRange")]
    [SerializeField] private Vector2 handleLocalZRange = new Vector2(-120f, 120f);
    [Tooltip("Degrees of handle rotation per pixel of horizontal mouse movement.")]
    [SerializeField, Min(0.01f)] private float mouseDegreesPerPixel = 0.35f;
    [Tooltip("The matching range on the separate full-rotation needle's selected global axis.")]
    [FormerlySerializedAs("needleGlobalXRange")]
    [FormerlySerializedAs("needleLocalZRange")]
    [SerializeField] private Vector2 needleGlobalYRange = new Vector2(0f, 360f);
    [Tooltip("Global axis used by the full-rotation NeedleRot object.")]
    [SerializeField] private EssenceRotationAxis fullNeedleRotationAxis = EssenceRotationAxis.Y;
    [Tooltip("Choose Local when you want only one local Inspector axis to change.")]
    [SerializeField] private EssenceRotationSpace fullNeedleRotationSpace = EssenceRotationSpace.Local;
    [SerializeField, Min(0.1f)] private float successTolerance = 1f;
    [Header("Debug")]
    [SerializeField] private bool logAlignmentDiagnostics = true;

    [Header("Indicator")]
    [SerializeField] private GameObject indicatorObject;
    [SerializeField] private Renderer indicatorRenderer;
    [SerializeField] private Material inactiveMaterial;
    [SerializeField] private Material readyMaterial;

    private float targetAngle;
    private float handleAngle;
    private bool isCalibrating;
    private bool hasTarget;
    private bool updatesIndicatorMaterial = true;

    public bool IsAligned { get; private set; }
    public float TargetAngle => targetAngle;
    public Material InactiveMaterial => inactiveMaterial;
    public Material ReadyMaterial => readyMaterial;
    public Renderer IndicatorRenderer => indicatorRenderer;
    public bool IsControlling => isCalibrating;
    public static EssenceNeedleController ActiveController { get; private set; }

    private void Awake()
    {
        if (handle == null)
            handle = transform;
        if (fullRotationNeedle == null)
            Debug.LogWarning("Essence needle requires a separate Full Rotation Needle transform.", this);
        else if (fullRotationNeedle == handle)
            Debug.LogError("Full Rotation Needle is assigned to the Handle. Assign the separate NeedleRot transform here.", this);
        handleAngle = NormalizeSignedAngle(GetLocalAngle(handle, handleRotationAxis));
        if (indicatorRenderer == null && indicatorObject != null)
            indicatorRenderer = indicatorObject.GetComponent<Renderer>();
        UpdateIndicator();
    }

    private void Update()
    {
        if (!isCalibrating || Mouse.current == null)
            return;

        float mouseDeltaX = Mouse.current.delta.ReadValue().x;
        if (!Mathf.Approximately(mouseDeltaX, 0f))
            SetHandleAngle(handleAngle - mouseDeltaX * mouseDegreesPerPixel);
    }

    private void OnDisable()
    {
        CancelControl();
    }

    /// <summary>Assigns a full-needle global-Y target.</summary>
    public float SetTargetAngle(float angle)
    {
        // The extractor's requested marker angle is authoritative. The
        // needle range maps handle travel to its visual rotation only; it
        // must not silently replace the requested pass value.
        targetAngle = angle;
        hasTarget = true;
        RefreshAlignment();
        return targetAngle;
    }

    /// <summary>Removes only the extractor target; the player may still move the handle.</summary>
    public void ClearTarget()
    {
        hasTarget = false;
        IsAligned = false;
        UpdateIndicator();
    }

    public void StopCalibrating()
    {
        isCalibrating = false;
        hasTarget = false;
        IsAligned = false;
        UpdateIndicator();
    }

    /// <summary>Stops camera/mouse control without clearing a green result.</summary>
    public void CancelControl()
    {
        isCalibrating = false;
        if (ActiveController == this)
            ActiveController = null;
        UpdateIndicator();
    }

    public void SetIndicatorMaterialUpdatesEnabled(bool enabled)
    {
        updatesIndicatorMaterial = enabled;
        UpdateIndicator();
    }

    public void Interact()
    {
        // The player may move the handle at any time. It only counts as a
        // successful extraction once the extractor has supplied a target.
        if (isCalibrating)
        {
            CancelControl();
            return;
        }

        isCalibrating = true;
        ActiveController = this;
        RefreshAlignment();
    }

    private void SetHandleAngle(float handleAngle)
    {
        this.handleAngle = Mathf.Clamp(handleAngle, handleLocalZRange.x, handleLocalZRange.y);
        SetLocalAngle(handle, handleRotationAxis, this.handleAngle);

        if (fullRotationNeedle == null || fullRotationNeedle == handle)
            return;

        float normalisedHandle = Mathf.InverseLerp(
            handleLocalZRange.x, handleLocalZRange.y, this.handleAngle);
        SetAngle(fullRotationNeedle, fullNeedleRotationAxis, fullNeedleRotationSpace, Mathf.Lerp(
            needleGlobalYRange.x, needleGlobalYRange.y, normalisedHandle));
        RefreshAlignment();
    }

    private void RefreshAlignment()
    {
        float current = fullRotationNeedle != null
            ? GetAngle(fullRotationNeedle, fullNeedleRotationAxis, fullNeedleRotationSpace)
            : 0f;
        float difference = Mathf.Abs(Mathf.DeltaAngle(current, targetAngle));
        IsAligned = hasTarget && isCalibrating && difference <= successTolerance;

        // Once the player reaches the green window, release the handle. Keep
        // IsAligned true so the extractor can begin without the player having
        // to hold or operate the control any further.
        if (IsAligned)
        {
            if (logAlignmentDiagnostics)
            {
                Debug.Log(
                    $"[EssenceNeedle] Accepted: target={targetAngle:F2}, current={current:F2}, " +
                    $"difference={difference:F2}, tolerance={successTolerance:F2}, " +
                    $"axis={fullNeedleRotationAxis}, space={fullNeedleRotationSpace}, " +
                    $"local={fullRotationNeedle?.localEulerAngles}, world={fullRotationNeedle?.eulerAngles}",
                    this);
            }
            CancelControl();
        }

        UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        if (!updatesIndicatorMaterial || indicatorRenderer == null)
            return;

        Material material = IsAligned ? readyMaterial : inactiveMaterial;
        if (material != null)
            indicatorRenderer.sharedMaterial = material;
    }

    private static float GetLocalAngle(Transform target, EssenceRotationAxis axis)
    {
        if (target == null)
            return 0f;
        return GetAxis(target.localEulerAngles, axis);
    }

    private static float GetAngle(Transform target, EssenceRotationAxis axis, EssenceRotationSpace space)
    {
        if (target == null)
            return 0f;
        return GetAxis(space == EssenceRotationSpace.Local ? target.localEulerAngles : target.eulerAngles, axis);
    }

    private static float GetAxis(Vector3 euler, EssenceRotationAxis axis) => axis switch
    {
        EssenceRotationAxis.X => euler.x,
        EssenceRotationAxis.Y => euler.y,
        _ => euler.z
    };

    private static void SetAngle(Transform target, EssenceRotationAxis axis, EssenceRotationSpace space, float angle)
    {
        if (target == null)
            return;

        Vector3 euler = space == EssenceRotationSpace.Local ? target.localEulerAngles : target.eulerAngles;
        SetAxis(ref euler, axis, angle);
        if (space == EssenceRotationSpace.Local)
            target.localEulerAngles = euler;
        else
            target.eulerAngles = euler;
    }

    private static void SetLocalAngle(Transform target, EssenceRotationAxis axis, float angle)
    {
        if (target == null)
            return;

        Vector3 euler = target.localEulerAngles;
        SetAxis(ref euler, axis, angle);
        target.localEulerAngles = euler;
    }

    private static void SetAxis(ref Vector3 euler, EssenceRotationAxis axis, float angle)
    {
        switch (axis)
        {
            case EssenceRotationAxis.X: euler.x = angle; break;
            case EssenceRotationAxis.Y: euler.y = angle; break;
            default: euler.z = angle; break;
        }
    }

    private static float NormalizeSignedAngle(float angle) => angle > 180f ? angle - 360f : angle;
}
