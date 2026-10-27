using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to the cauldron's ingredient-deposit BoxCollider. It accepts dried
/// any ICauldronIngredient, records it on the parent Cauldron, then consumes it.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CauldronIngredientDeposit : MonoBehaviour
{
    [Header("SFX")]
    [SerializeField] private AudioClip ingredientPlopSfx;
    [SerializeField, Range(0f, 1f)] private float ingredientPlopVolume = 0.8f;

    private readonly HashSet<MonoBehaviour> claimedIngredients = new HashSet<MonoBehaviour>();
    private Cauldron cauldron;

    private void Awake()
    {
        cauldron = GetComponentInParent<Cauldron>();
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (cauldron == null)
            return;

        if (!TryGetIngredient(other, out MonoBehaviour ingredientObject,
                out ICauldronIngredient ingredient) ||
            !claimedIngredients.Add(ingredientObject))
            return;

        if (cauldron.TryAddIngredient(ingredient))
        {
            SfxAudio.PlayAt(ingredientPlopSfx, transform.position, ingredientPlopVolume);
            Destroy(ingredientObject.gameObject);
        }
        else
        {
            claimedIngredients.Remove(ingredientObject);
        }
    }

    private static bool TryGetIngredient(
        Collider other,
        out MonoBehaviour ingredientObject,
        out ICauldronIngredient ingredient)
    {
        foreach (MonoBehaviour component in other.GetComponentsInParent<MonoBehaviour>())
        {
            if (component is ICauldronIngredient cauldronIngredient)
            {
                ingredientObject = component;
                ingredient = cauldronIngredient;
                return true;
            }
        }

        ingredientObject = null;
        ingredient = null;
        return false;
    }

    private void OnValidate()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
            boxCollider.isTrigger = true;
    }
}
