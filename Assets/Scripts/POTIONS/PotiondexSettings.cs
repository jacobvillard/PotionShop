using UnityEngine;

/// <summary>Inspector controls for Canvas/RootCodex. Changes are applied live while playing.</summary>
public sealed class PotiondexSettings : MonoBehaviour
{
    [Header("Opening")]
    public bool defaultOpen;
    [Tooltip("Testing only: reveals every formula without brewing its prerequisites.")]
    public bool unlockAllForTesting;

    [Header("Section titles")]
    public Vector2 sectionTitleOffset = Vector2.zero;

    [Header("Opening view")]
    public Vector2 openingMapPosition = new Vector2(0f, 400f);
    [Range(.2f, 3f)] public float openingZoom = 1f;

    [Header("Grid spacing")]
    [Min(120f)] public float horizontalBoxGap = 370f;
    [Min(120f)] public float verticalBoxGap = 190f;
    [Min(300f)] public float sectionGap = 700f;
    [Min(.1f)] public float dragSpeed = 1f;

    [Header("Zoom")]
    [Range(.2f, 1f)] public float minimumZoom = .55f;
    [Range(1f, 4f)] public float maximumZoom = 1.6f;
    [Range(.01f, .3f)] public float zoomStep = .1f;

    [Header("Tooltip")]
    [Range(.5f, 2f)] public float tooltipScale = 1f;

    [Header("Background flare")]
    public Sprite magicalFlareSprite;
    public Color magicalFlareColor = new Color(.58f, .35f, .9f, .12f);
    [Min(100f)] public float magicalFlareSize = 620f;

    [Header("Arcane frame assets")]
    public Sprite borderLineSprite;
    public Sprite borderCrestSprite;
    public Sprite borderGemSprite;

    [Header("Unlock reveal")]
    [Min(.1f)] public float revealTravelDuration = .8f;
    [Min(.05f)] public float revealPulseDuration = .35f;
    public Color unlockGold = new Color(1f, .72f, .2f, 1f);
    public AudioClip unlockSfx;

    [Header("Unlocked node frame")]
    [Tooltip("Distance of the gold frame from the 112px node backing. Lower values pull it inward.")]
    [Range(-8f, 12f)] public float unlockedBorderInset = 3f;
    [Range(1f, 10f)] public float unlockedBorderThickness = 3f;
    [Range(3f, 18f)] public float unlockedBorderCornerSize = 7f;

    private void OnValidate()
    {
        maximumZoom = Mathf.Max(maximumZoom, minimumZoom);
        openingZoom = Mathf.Clamp(openingZoom, minimumZoom, maximumZoom);
        verticalBoxGap = Mathf.Max(verticalBoxGap, 120f);
        horizontalBoxGap = Mathf.Max(horizontalBoxGap, 120f);
        // Restoration uses three rows, so smaller values make adjacent sections overlap.
        sectionGap = Mathf.Max(sectionGap, verticalBoxGap * 3f);
    }
}
