using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class IngredientDetector : MonoBehaviour
{
    [SerializeField] private TMP_Text ingredientText;
    [SerializeField, TextArea] private string defaultText =
        "Place object here to detect INGREDIENTS";
    [SerializeField] private string emptyCauldronText = "No ingredients detected.";
    [Header("Chalkboard SFX")]
    [SerializeField] private AudioClip textChangedSfx;
    [SerializeField, Range(0f, 1f)] private float textChangedSfxVolume = 0.5f;
    [SerializeField, Min(0f)] private float textChangedSfxCooldown = 0.4f;

    private readonly HashSet<Cauldron> detectedCauldrons = new();
    private readonly HashSet<Petal> detectedPetals = new();
    private readonly StringBuilder textBuilder = new();
    private string displayedText;
    private bool hasInitialisedDisplay;
    private float lastTextSfxTime = float.NegativeInfinity;

    private void Awake()
    {
        if (ingredientText == null)
            ingredientText = GetComponentInChildren<TMP_Text>(true);

        RefreshText();
        hasInitialisedDisplay = true;
    }

    private void Update()
    {
        detectedCauldrons.RemoveWhere(cauldron => cauldron == null);
        detectedPetals.RemoveWhere(petal => petal == null);
        RefreshText();
    }

    private void OnTriggerEnter(Collider other)
    {
        AddCauldron(other);
        AddPetal(other);
    }

    private void OnTriggerExit(Collider other)
    {
        Cauldron cauldron = other.GetComponentInParent<Cauldron>();

        if (cauldron != null)
            detectedCauldrons.Remove(cauldron);

        Petal petal = other.GetComponentInParent<Petal>();

        if (petal != null)
            detectedPetals.Remove(petal);
    }

    private void AddCauldron(Collider other)
    {
        Cauldron cauldron = other.GetComponentInParent<Cauldron>();

        if (cauldron != null)
            detectedCauldrons.Add(cauldron);
    }

    private void AddPetal(Collider other)
    {
        Petal petal = other.GetComponentInParent<Petal>();

        if (petal != null)
            detectedPetals.Add(petal);
    }

    private void RefreshText()
    {
        string nextText = detectedCauldrons.Count > 0
            ? BuildIngredientList(GetDetectedCauldron())
            : detectedPetals.Count > 0
                ? BuildPetalDetails(GetDetectedPetal())
                : defaultText;

        if (ingredientText == null || nextText == displayedText)
            return;

        ingredientText.text = nextText;
        displayedText = nextText;

        if (hasInitialisedDisplay && Time.time >= lastTextSfxTime + textChangedSfxCooldown)
        {
            SfxAudio.PlayAt(textChangedSfx, transform.position, textChangedSfxVolume);
            lastTextSfxTime = Time.time;
        }
    }

    private Cauldron GetDetectedCauldron()
    {
        foreach (Cauldron cauldron in detectedCauldrons)
            return cauldron;

        return null;
    }

    private Petal GetDetectedPetal()
    {
        foreach (Petal petal in detectedPetals)
            return petal;

        return null;
    }

    private string BuildIngredientList(Cauldron cauldron)
    {
        if (cauldron == null)
            return defaultText;

        textBuilder.Clear();
        textBuilder.AppendLine("INGREDIENTS:");

        bool hasIngredients = false;

        foreach (LiquidIngredient ingredient in System.Enum.GetValues(
                     typeof(LiquidIngredient)))
        {
            float amount = cauldron.GetLiquidAmount(ingredient);

            if (amount <= Mathf.Epsilon)
                continue;

            textBuilder.Append("• ");
            textBuilder.Append(ingredient);
            textBuilder.Append(": ");
            textBuilder.Append(amount.ToString("0.##"));
            textBuilder.AppendLine();
            hasIngredients = true;
        }

        foreach (Cauldron.AdditionalIngredientContent ingredient in cauldron.AdditionalIngredients)
        {
            if (ingredient == null || ingredient.amount <= 0)
                continue;

            textBuilder.Append("• ");
            textBuilder.Append(ingredient.displayName);
            textBuilder.Append(": ");
            textBuilder.Append(ingredient.amount);
            textBuilder.AppendLine();
            hasIngredients = true;
        }

        if (hasIngredients)
        {
            textBuilder.Append("• Boil Status: ");
            textBuilder.Append(cauldron.IsBoiled ? "Boiled" : "Not Boiled");
            textBuilder.AppendLine();
        }

        return hasIngredients ? textBuilder.ToString().TrimEnd() : emptyCauldronText;
    }

    private string BuildPetalDetails(Petal petal)
    {
        if (petal == null)
            return defaultText;

        textBuilder.Clear();
        textBuilder.AppendLine("INGREDIENT:");
        textBuilder.Append("• ");
        textBuilder.Append(FormatPetalType(petal.Type));
        textBuilder.AppendLine(" Petal");
        textBuilder.Append("• State: ");
        textBuilder.Append(petal.State);
        return textBuilder.ToString();
    }

    private static string FormatPetalType(PetalType petalType)
    {
        return petalType switch
        {
            PetalType.WhiteDaisy => "White Daisy",
            PetalType.PinkDaisy => "Pink Daisy",
            PetalType.Rose => "Rose",
            _ => petalType.ToString()
        };
    }

}
