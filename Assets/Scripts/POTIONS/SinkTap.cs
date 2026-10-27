using System.Collections;
using UnityEngine;

public class SinkTap : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private Sink sink;

    [Header("Liquid")]
    [SerializeField] private LiquidIngredient liquidType = LiquidIngredient.Water;
    [SerializeField] private float fillRate = 0.2f;

    [Header("Water Stream")]
    [SerializeField] private GameObject waterStream;
    [SerializeField] private Renderer waterStreamRenderer;

    [SerializeField] private string fillProperty = "_Fill";

    [Header("Animation")]
    [SerializeField] private float turnOnDuration = 0.2f;
    [SerializeField] private float turnOffDuration = 0.25f;

    [Header("SFX")]
    [SerializeField] private AudioClip runningWaterSfx;
    [SerializeField, Range(0f, 1f)] private float runningWaterVolume = 0.7f;

    private MaterialPropertyBlock propertyBlock;
    private Coroutine fillRoutine;
    private AudioSource runningWaterSource;

    public bool IsRunning { get; private set; }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        SetStreamFill(0f);

        if (waterStream != null)
            waterStream.SetActive(false);

        runningWaterSource = SfxAudio.CreateLoopSource(
            gameObject, runningWaterSfx, runningWaterVolume);
    }

    public void Use()
    {
        // Cannot manually switch it off once filling begins.
        if (IsRunning)
            return;

        if (!sink.HasLockedObject)
            return;

        ISinkable target = sink.LockedObject;

        if (!target.CanReceiveLiquid(liquidType))
            return;

        fillRoutine = StartCoroutine(FillSequence());
    }

    private IEnumerator FillSequence()
    {
        IsRunning = true;

        // Lock the sink immediately.
        sink.BeginFill();

        if (sink.LockedObject is Cauldron cauldron)
            cauldron.BeginFilling();

        // ---------------------------
        // TURN TAP ON
        // ---------------------------

        waterStream.SetActive(true);
        SfxAudio.RouteToSfxMixer(runningWaterSource);
        runningWaterSource?.Play();

        yield return AnimateStream(0f, 1f, turnOnDuration);

        // ---------------------------
        // FILL OBJECT
        // ---------------------------

        while (sink.HasLockedObject)
        {
            ISinkable target = sink.LockedObject;

            if (!target.CanReceiveLiquid(liquidType))
                break;

            target.AddLiquid(
                liquidType,
                fillRate * Time.deltaTime
            );

            yield return null;
        }

        // ---------------------------
        // TURN TAP OFF
        // ---------------------------

        yield return AnimateStream(1f, 0f, turnOffDuration);

        waterStream.SetActive(false);
        runningWaterSource?.Stop();

        // NOW the object can be removed.
        if (sink.LockedObject is Cauldron finishedCauldron)
            finishedCauldron.FinishFilling();

        sink.EndFill();

        IsRunning = false;
        fillRoutine = null;
    }

    private IEnumerator AnimateStream(
        float from,
        float to,
        float duration)
    {
        if (duration <= 0f)
        {
            SetStreamFill(to);
            yield break;
        }

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / duration);

            // Slightly smoother than a straight linear movement.
            t = Mathf.SmoothStep(0f, 1f, t);

            SetStreamFill(
                Mathf.Lerp(from, to, t)
            );

            yield return null;
        }

        SetStreamFill(to);
    }

    private void SetStreamFill(float amount)
    {
        if (waterStreamRenderer == null)
            return;

        waterStreamRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetFloat(
            fillProperty,
            Mathf.Clamp01(amount)
        );

        waterStreamRenderer.SetPropertyBlock(propertyBlock);
    }

    public void Interact() {
        Use();
    }
}
