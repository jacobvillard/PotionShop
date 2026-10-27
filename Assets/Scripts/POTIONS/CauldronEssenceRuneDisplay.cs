using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Attach to a world-space TMP Canvas on a Cauldron prefab. It displays one
/// rune per extracted essence and keeps the canvas facing the player camera.
/// Honey is B; Milk is M.
/// </summary>
public class CauldronEssenceRuneDisplay : MonoBehaviour
{
    [System.Serializable]
    private class EssenceRune
    {
        [Tooltip("The matching Cauldron additional-ingredient ID, e.g. essence_honey.")]
        public string essenceId;
        [Tooltip("Text shown for this essence.")]
        public string rune;
        [Tooltip("Face colour for this rune.")]
        public Color colour = Color.white;
    }

    [SerializeField] private Cauldron cauldron;
    [SerializeField] private TMP_Text runeText;
    [Tooltip("Extra authored TMP labels, for example one on the opposite side of the cauldron.")]
    [SerializeField] private TMP_Text[] additionalRuneTexts = new TMP_Text[0];
    [SerializeField] private bool hideWhenEmpty = true;
    [Tooltip("Optional TMP material preset with its Glow settings configured.")]
    [SerializeField] private Material glowingTextMaterial;
    [Header("Glow Pulse")]
    [SerializeField] private bool pulseGlow = true;
    [SerializeField, Range(0f, 1f)] private float minimumGlowPower = 0.1f;
    [SerializeField, Range(0f, 1f)] private float maximumGlowPower = 0.3f;
    [SerializeField, Min(0.01f)] private float glowPulseFrequency = 1f;
    [Header("Rune Definitions")]
    [SerializeField] private List<EssenceRune> runeDefinitions = new()
    {
        new EssenceRune { essenceId = "essence_honey", rune = "B", colour = new Color(1f, 0.7f, 0.1f) },
        new EssenceRune { essenceId = "essence_milk", rune = "M" }
    };

    private readonly StringBuilder runeBuilder = new();
    private string displayedRunes;
    private bool hasDisplayedEssence;
    private Material runtimeGlowMaterial;
    private static readonly int GlowPowerProperty = Shader.PropertyToID("_GlowPower");

    private void Awake()
    {
        if (cauldron == null)
            cauldron = GetComponentInParent<Cauldron>();
        if (runeText == null)
            runeText = GetComponentInChildren<TMP_Text>(true);

        if (glowingTextMaterial != null)
        {
            runtimeGlowMaterial = new Material(glowingTextMaterial)
            {
                name = $"{glowingTextMaterial.name} (Cauldron Rune Instance)"
            };
        }
        ConfigureText(runeText);
        foreach (TMP_Text additionalText in additionalRuneTexts)
            ConfigureText(additionalText);

        RefreshRunes();
    }

    private void LateUpdate()
    {
        RefreshRunes();
        UpdateGlowPulse();
    }

    private void OnDestroy()
    {
        if (runtimeGlowMaterial != null)
            Destroy(runtimeGlowMaterial);
    }

    private void RefreshRunes()
    {
        if (cauldron == null)
            return;

        runeBuilder.Clear();
        foreach (EssenceRune definition in runeDefinitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.essenceId) ||
                string.IsNullOrWhiteSpace(definition.rune))
                continue;

            AppendRuneIfPresent(
                definition.essenceId,
                $"<color=#{ColorUtility.ToHtmlStringRGBA(definition.colour)}>{definition.rune}</color>");
        }

        string nextRunes = runeBuilder.ToString();

        // Leave the prefab's authored sample text alone until this cauldron
        // has actually received an essence. This makes material/layout work
        // visible while setting up the prefab.
        if (string.IsNullOrEmpty(nextRunes) && !hasDisplayedEssence)
            return;

        if (!string.IsNullOrEmpty(nextRunes))
            hasDisplayedEssence = true;

        if (nextRunes == displayedRunes)
            return;

        displayedRunes = nextRunes;
        ApplyRunes(runeText);
        foreach (TMP_Text additionalText in additionalRuneTexts)
            ApplyRunes(additionalText);
    }

    private void AppendRuneIfPresent(string essenceId, string rune)
    {
        if (cauldron.GetAdditionalIngredientAmount(essenceId) <= 0)
            return;

        runeBuilder.Append(rune);
    }

    private void ConfigureText(TMP_Text text)
    {
        if (text == null)
            return;

        // Rune colours are supplied with <color> tags. A white base and
        // disabled override preserve the per-rune colour values.
        text.color = Color.white;
        text.overrideColorTags = false;
        if (runtimeGlowMaterial != null)
        {
            text.fontSharedMaterial = runtimeGlowMaterial;
            // TMP needs regenerated padding for an outline/glow material;
            // otherwise the glow can be clipped by the text mesh bounds.
            text.UpdateMeshPadding();
        }
    }

    private void ApplyRunes(TMP_Text text)
    {
        if (text == null)
            return;

        text.text = displayedRunes;
        if (hideWhenEmpty)
            text.gameObject.SetActive(!string.IsNullOrEmpty(displayedRunes));
    }

    private void UpdateGlowPulse()
    {
        if (!pulseGlow || runtimeGlowMaterial == null ||
            !runtimeGlowMaterial.HasProperty(GlowPowerProperty))
            return;

        float wave = (Mathf.Sin(Time.time * glowPulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        runtimeGlowMaterial.SetFloat(
            GlowPowerProperty,
            Mathf.Lerp(minimumGlowPower, maximumGlowPower, wave));
    }
}
