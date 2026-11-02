using TMPro;
using UnityEngine;

/// <summary>Chooses a fresh cosy tagline whenever the title screen opens.</summary>
[RequireComponent(typeof(TMP_Text))]
public class TitleScreenSubtitleRandomizer : MonoBehaviour
{
    private static readonly string[] Taglines =
    {
        "A little shop. A lot of magic.",
        "Where every bottle holds a little magic.",
        "A cosy little corner of alchemy.",
        "A pinch of magic in every bottle.",
        "Big dreams, little potions.",
        "A little magic goes a long way.",
        "Magic doesn't bottle itself!",
        "A potion for every occasion.",
        "Carefully brewed. Questionably tested.",
        "Some side effects may include magic.",
        "Brewing trouble, one bottle at a time.",
        "No refunds on mysterious transformations.",
        "Every potion has its place.",
        "A whole world of magic to bottle.",
        "One bottle closer to perfection.",
        "Fill your shelves with wonder.",
        "Discover. Brew. Collect.",
        "There's always another potion to discover.",
        "Small shop. Grand enchantments.",
        "Stir well. Dream big.",
        "Wonder, one bottle at a time.",
        "Your next favourite potion awaits."
    };

    private void Awake()
    {
        TMP_Text subtitle = GetComponent<TMP_Text>();
        subtitle.text = Taglines[Random.Range(0, Taglines.Length)];
    }
}
