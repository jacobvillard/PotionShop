using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Staff : MonoBehaviour, IUseable
{
    [SerializeField] private float cooldown = 0.5f;

    [Header("Cast Timing")]
    [Tooltip("How long the staff charges before firing the lightning.")]
    [SerializeField, Min(0f)] private float warmupDuration = 2f;
    [SerializeField, Min(0f)] private float lightningEffectDuration = 1f;
    [SerializeField, Min(0.02f)] private float electricityFlickerInterval = 0.1f;

    [Header("Warm-up Effects")]
    [SerializeField] private GameObject implosionEffect;
    [SerializeField] private GameObject staffGlowEffect;
    [Tooltip("These systems flicker on and off together throughout the warm-up.")]
    [SerializeField] private ParticleSystem[] sparkEffects;
    [Tooltip("These systems burst once when the lightning fires.")]
    [SerializeField] private ParticleSystem[] electricityEffects;

    [Header("Warm-up Audio")]
    [SerializeField] private AudioSource spellAudioSource;
    [SerializeField] private AudioClip[] sparkSfx;
    [SerializeField] private AudioClip[] lightningSfx;
    [SerializeField] private Vector2 sfxPitchRange = new Vector2(0.9f, 1.1f);

    [Header("Scene Light Dimming")]
    [Tooltip("Non-staff lights fade to this fraction of their original intensity during the warm-up.")]
    [SerializeField, Range(0f, 1f)] private float sceneLightDimMultiplier = 0.25f;
    [Tooltip("Unity's ambient and reflection lighting fade to this fraction during the warm-up.")]
    [SerializeField, Range(0f, 1f)] private float ambientLightDimMultiplier = 0.1f;
    [SerializeField, Min(1)] private int restoreLightFlickerCount = 3;
    [SerializeField, Min(0.01f)] private float restoreLightFlickerInterval = 0.08f;

    [Header("Post-processing Dimming")]
    [Tooltip("The camera to darken. If empty, the main camera is used.")]
    [SerializeField] private Camera postProcessCamera;
    [Tooltip("Exposure in EV at the darkest point. Lower values create a stronger screen-wide blackout.")]
    [SerializeField, Range(-10f, 0f)] private float warmupPostExposure = -3.5f;

    [Header("Lightning")]
    [Tooltip("Place this child transform at the emitting end of the staff.")]
    [SerializeField] private Transform lightningStartPoint;
    [Tooltip("The Zap object already placed in the scene. If empty, the object named below is used.")]
    [SerializeField] private Transform zapEffect;
    [SerializeField] private string zapObjectName = "VFX_Zap_02_Blue";
    [SerializeField, Min(0.1f)] private float maxLightningDistance = 20f;
    [Tooltip("The Zap asset's unscaled length along its local Y axis.")]
    [SerializeField, Min(0.01f)] private float zapBaseLength = 1f;
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("Prevents the ground-impact systems in the Zap prefab from drawing a flat mark.")]
    [SerializeField] private bool suppressGroundEffects = true;

    private float nextUseTime;

    private ParticleSystem[] zapParticleSystems;
    private readonly Dictionary<Light, float> originalLightIntensities = new();
    private bool isCharging;
    private float originalAmbientIntensity;
    private float originalReflectionIntensity;
    private bool hasSavedRenderSettings;
    private Volume spellPostProcessVolume;
    private ColorAdjustments spellColorAdjustments;

    public void Use()
    {
        if (isCharging || Time.time < nextUseTime)
            return;

        nextUseTime = Time.time + Mathf.Max(cooldown, warmupDuration + lightningEffectDuration);
        StartCoroutine(WarmUpAndFire());
    }

    private void Awake()
    {
        FindZapEffect();

        if (zapEffect != null)
        {
            StopZapParticles();
            zapEffect.gameObject.SetActive(false);
        }

        SetEffectActive(implosionEffect, false);
        SetEffectActive(staffGlowEffect, false);
        SetParticleEffectsActive(sparkEffects, false);
        SetParticleEffectsActive(electricityEffects, false);
        SetupPostProcessing();
    }

    private IEnumerator WarmUpAndFire()
    {
        if (isCharging)
            yield break;

        isCharging = true;
        CacheSceneLightIntensities();

        SetEffectActive(implosionEffect, true);
        SetEffectActive(staffGlowEffect, true);

        float elapsed = 0f;
        float nextFlickerTime = 0f;
        bool sparksOn = false;

        while (elapsed < warmupDuration)
        {
            elapsed += Time.deltaTime;
            ApplySceneLightDimming(Mathf.Clamp01(elapsed / warmupDuration));

            if (elapsed >= nextFlickerTime)
            {
                sparksOn = !sparksOn;
                SetParticleEffectsActive(sparkEffects, sparksOn);
                PlayRandomSfx(sparkSfx);
                nextFlickerTime = elapsed + electricityFlickerInterval;
            }

            yield return null;
        }

        SetParticleEffectsActive(sparkEffects, false);
        SetParticleEffectsActive(electricityEffects, true);
        FireLightning();
        PlayRandomSfx(lightningSfx);

        yield return StartCoroutine(FlickerLightsBack());

        float remainingLightningDuration = lightningEffectDuration -
            restoreLightFlickerCount * restoreLightFlickerInterval;
        if (remainingLightningDuration > 0f)
            yield return new WaitForSeconds(remainingLightningDuration);

        ResetCastState();
    }

    private void FireLightning()
    {
        FindZapEffect();

        if (zapEffect == null)
        {
            Debug.LogWarning($"{name}: No Zap effect assigned. Assign the scene Zap object to the Staff component.", this);
            return;
        }

        Transform start = lightningStartPoint != null ? lightningStartPoint : transform;
        Vector3 origin = start.position;
        Vector3 direction = start.forward;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        float distance = maxLightningDistance;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxLightningDistance, hitMask, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            // The staff's own collider is held in front of the player, so do not let it block the cast.
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < distance)
                distance = hit.distance;
        }

        zapEffect.position = origin;
        zapEffect.rotation = Quaternion.FromToRotation(Vector3.up, direction);

        Vector3 scale = zapEffect.localScale;
        scale.y = distance / zapBaseLength;
        zapEffect.localScale = scale;

        zapEffect.gameObject.SetActive(true);
        foreach (ParticleSystem particleSystem in zapParticleSystems)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (IsGroundEffect(particleSystem))
                continue;

            particleSystem.Play(true);
        }
    }

    private void FindZapEffect()
    {
        if (zapEffect == null && !string.IsNullOrWhiteSpace(zapObjectName))
        {
            GameObject zapObject = GameObject.Find(zapObjectName);
            if (zapObject != null)
                zapEffect = zapObject.transform;
        }

        if (zapEffect != null && zapParticleSystems == null)
            zapParticleSystems = zapEffect.GetComponentsInChildren<ParticleSystem>(true);
    }

    private void StopZapParticles()
    {
        foreach (ParticleSystem particleSystem in zapParticleSystems)
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private bool IsGroundEffect(ParticleSystem particleSystem)
    {
        if (!suppressGroundEffects)
            return false;

        string effectName = particleSystem.name;
        return effectName.Contains("Floor") ||
               effectName.Contains("Scorch") ||
               effectName.Contains("BG");
    }

    private void CacheSceneLightIntensities()
    {
        originalLightIntensities.Clear();
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        hasSavedRenderSettings = true;

        foreach (Light sceneLight in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!sceneLight.transform.IsChildOf(transform))
                originalLightIntensities.Add(sceneLight, sceneLight.intensity);
        }
    }

    private void ApplySceneLightDimming(float progress)
    {
        foreach (KeyValuePair<Light, float> lightEntry in originalLightIntensities)
        {
            if (lightEntry.Key != null)
            {
                lightEntry.Key.intensity = Mathf.Lerp(
                    lightEntry.Value,
                    lightEntry.Value * sceneLightDimMultiplier,
                    progress);
            }
        }

        if (hasSavedRenderSettings)
        {
            RenderSettings.ambientIntensity = Mathf.Lerp(
                originalAmbientIntensity,
                originalAmbientIntensity * ambientLightDimMultiplier,
                progress);
            RenderSettings.reflectionIntensity = Mathf.Lerp(
                originalReflectionIntensity,
                originalReflectionIntensity * ambientLightDimMultiplier,
                progress);
        }

        if (spellColorAdjustments != null)
            spellColorAdjustments.postExposure.value = Mathf.Lerp(0f, warmupPostExposure, progress);
    }

    private IEnumerator FlickerLightsBack()
    {
        for (int flicker = 0; flicker < restoreLightFlickerCount; flicker++)
        {
            bool useOriginalLighting = flicker % 2 == 0;
            ApplySceneLightDimming(useOriginalLighting ? 0f : 1f);
            yield return new WaitForSeconds(restoreLightFlickerInterval);
        }

        ApplySceneLightDimming(0f);
    }

    private void ResetCastState()
    {
        if (zapEffect != null)
        {
            StopZapParticles();
            zapEffect.gameObject.SetActive(false);
        }

        SetEffectActive(implosionEffect, false);
        SetEffectActive(staffGlowEffect, false);
        SetParticleEffectsActive(sparkEffects, false);
        SetParticleEffectsActive(electricityEffects, false);

        foreach (KeyValuePair<Light, float> lightEntry in originalLightIntensities)
        {
            if (lightEntry.Key != null)
                lightEntry.Key.intensity = lightEntry.Value;
        }

        if (hasSavedRenderSettings)
        {
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            RenderSettings.reflectionIntensity = originalReflectionIntensity;
            hasSavedRenderSettings = false;
        }

        if (spellColorAdjustments != null)
            spellColorAdjustments.postExposure.value = 0f;

        originalLightIntensities.Clear();
        isCharging = false;
    }

    private void SetEffectActive(GameObject effect, bool active)
    {
        if (effect != null)
            effect.SetActive(active);
    }

    private void SetParticleEffectsActive(ParticleSystem[] effects, bool active)
    {
        if (effects == null)
            return;

        foreach (ParticleSystem effect in effects)
        {
            if (effect == null)
                continue;

            effect.gameObject.SetActive(active);

            if (active)
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.Play(true);
            }
            else
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void PlayRandomSfx(AudioClip[] clips)
    {
        if (spellAudioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;

        spellAudioSource.pitch = Random.Range(sfxPitchRange.x, sfxPitchRange.y);
        spellAudioSource.PlayOneShot(clip);
    }

    private void SetupPostProcessing()
    {
        if (spellPostProcessVolume != null)
            return;

        if (postProcessCamera == null)
            postProcessCamera = Camera.main;

        if (postProcessCamera == null)
            postProcessCamera = FindFirstObjectByType<Camera>();

        if (postProcessCamera == null)
        {
            Debug.LogWarning($"{name}: No camera was found for the lightning post-processing effect.", this);
            return;
        }

        UniversalAdditionalCameraData cameraData =
            postProcessCamera.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null)
            cameraData = postProcessCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        cameraData.renderPostProcessing = true;

        GameObject volumeObject = new GameObject("Staff Lightning Post Process");
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        spellPostProcessVolume = volumeObject.AddComponent<Volume>();
        spellPostProcessVolume.isGlobal = true;
        spellPostProcessVolume.priority = 100f;
        spellPostProcessVolume.sharedProfile = profile;

        spellColorAdjustments = profile.Add<ColorAdjustments>(true);
        spellColorAdjustments.postExposure.Override(0f);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetCastState();
    }
}
