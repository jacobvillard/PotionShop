using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LiquidRequirement
{
    public LiquidIngredient ingredient;
    [Min(0f)] public float amount = 1f;
}

[Serializable]
public class PetalRequirement
{
    public PetalType type;
    public PetalState state = PetalState.Dried;
    [Min(1)] public int amount = 1;
}

[Serializable]
public class AdditionalIngredientRequirement
{
    [Tooltip("The ingredient ID supplied by an ICauldronIngredient implementation.")]
    public string ingredientId;
    [Min(1)] public int amount = 1;
}

[Serializable]
public class PotionRecipe
{
    public string potionName = "New Potion";
    public GameObject potionPrefab;
    public bool requiresBoiled = true;
    public List<LiquidRequirement> liquids = new List<LiquidRequirement>();
    public List<PetalRequirement> petals = new List<PetalRequirement>();
    [Tooltip("For future non-petal ICauldronIngredient types.")]
    public List<AdditionalIngredientRequirement> additionalIngredients =
        new List<AdditionalIngredientRequirement>();
}

/// <summary>
/// Global list of exact cauldron recipes. Create/edit the shared asset under
/// Assets/Resources/PotionRecipeBook.asset.
/// </summary>
[CreateAssetMenu(fileName = "PotionRecipeBook", menuName = "Potions/Potion Recipe Book")]
public class PotionRecipeBook : ScriptableObject
{
    [SerializeField] private List<PotionRecipe> recipes = new List<PotionRecipe>();

    public PotionRecipe FindByName(string potionName)
    {
        return recipes.Find(recipe => recipe != null &&
            string.Equals(recipe.potionName, potionName, StringComparison.OrdinalIgnoreCase));
    }

    public static string DescribeIngredients(PotionRecipe recipe)
    {
        if (recipe == null) return "Formula data not found.";
        List<string> ingredients = new List<string>();
        foreach (LiquidRequirement liquid in recipe.liquids)
            if (liquid != null && liquid.amount > 0f) ingredients.Add($"{liquid.amount:0.#} × {liquid.ingredient}");
        foreach (PetalRequirement petal in recipe.petals)
            if (petal != null && petal.amount > 0) ingredients.Add($"{petal.amount} × {petal.state} {petal.type} petal");
        foreach (AdditionalIngredientRequirement item in recipe.additionalIngredients)
            if (item != null && !string.IsNullOrWhiteSpace(item.ingredientId)) ingredients.Add($"{item.amount} × {Readable(item.ingredientId)}");
        return ingredients.Count == 0 ? "No ingredients recorded." : string.Join("\n", ingredients);
    }

    private static string Readable(string id) => id.Replace("ingredient:", "").Replace("essence_", "")
        .Replace(':', ' ').Replace('-', ' ').Replace('_', ' ');

    public bool TryGetMatchingRecipe(Cauldron cauldron, out PotionRecipe matchingRecipe)
    {
        foreach (PotionRecipe recipe in recipes)
        {
            if (recipe != null && recipe.potionPrefab != null && Matches(recipe, cauldron))
            {
                matchingRecipe = recipe;
                return true;
            }
        }

        matchingRecipe = null;
        return false;
    }

    private static bool Matches(PotionRecipe recipe, Cauldron cauldron)
    {
        if (recipe.requiresBoiled != cauldron.IsBoiled)
            return false;

        Dictionary<LiquidIngredient, float> expectedLiquids = new Dictionary<LiquidIngredient, float>();
        foreach (LiquidRequirement requirement in recipe.liquids)
        {
            if (requirement == null)
                continue;

            expectedLiquids.TryGetValue(requirement.ingredient, out float currentAmount);
            expectedLiquids[requirement.ingredient] = currentAmount + requirement.amount;
        }

        foreach (LiquidIngredient ingredient in Enum.GetValues(typeof(LiquidIngredient)))
        {
            expectedLiquids.TryGetValue(ingredient, out float expectedAmount);
            if (!Mathf.Approximately(cauldron.GetLiquidAmount(ingredient), expectedAmount))
                return false;
        }

        Dictionary<string, int> expectedIngredients = new Dictionary<string, int>();
        foreach (PetalRequirement requirement in recipe.petals)
        {
            if (requirement == null)
                continue;

            AddExpectedIngredient(
                expectedIngredients,
                Petal.GetCauldronIngredientId(requirement.type, requirement.state),
                requirement.amount);
        }

        foreach (AdditionalIngredientRequirement requirement in recipe.additionalIngredients)
        {
            if (requirement == null || string.IsNullOrWhiteSpace(requirement.ingredientId))
                continue;

            AddExpectedIngredient(expectedIngredients, requirement.ingredientId, requirement.amount);
        }

        foreach (Cauldron.AdditionalIngredientContent ingredient in cauldron.AdditionalIngredients)
        {
            if (ingredient == null || ingredient.amount <= 0)
                continue;

            if (!expectedIngredients.TryGetValue(ingredient.ingredientId, out int expectedAmount) ||
                expectedAmount != ingredient.amount)
                return false;

            expectedIngredients.Remove(ingredient.ingredientId);
        }

        return expectedIngredients.Count == 0;
    }

    private static void AddExpectedIngredient(
        Dictionary<string, int> ingredients,
        string ingredientId,
        int amount)
    {
        if (string.IsNullOrWhiteSpace(ingredientId) || amount <= 0)
            return;

        ingredients.TryGetValue(ingredientId, out int currentAmount);
        ingredients[ingredientId] = currentAmount + amount;
    }
}
