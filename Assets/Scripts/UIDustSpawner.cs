using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a lightweight field of warm, drifting UI dust from a sprite sheet.
/// Attach it to a Canvas; the dust is drawn above the render texture but behind the menu artwork.
/// </summary>
public class UIDustSpawner : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private Texture2D dustSpriteSheet;
    [SerializeField] private RectTransform magicCircle;
    [SerializeField, Min(1)] private int spriteSheetColumns = 4;
    [SerializeField, Min(1)] private int spriteSheetRows = 3;
    [SerializeField, Min(1)] private int dustCount = 42;
    [SerializeField] private Color dustColor = new Color(0.86f, 0.78f, 0.59f, 0.28f);
    [SerializeField] private Vector2 sizeRange = new Vector2(2f, 7f);

    [Header("Movement")]
    [SerializeField] private Vector2 fallSpeedRange = new Vector2(7f, 22f);
    [SerializeField] private Vector2 sidewaysDriftRange = new Vector2(2f, 11f);
    [SerializeField, Min(0f)] private float respawnPadding = 18f;
    [SerializeField, Range(0, 20)] private int canvasSiblingIndex = 5;

    private readonly List<DustMote> motes = new List<DustMote>();
    private RectTransform dustLayer;
    private readonly List<Sprite> dustSprites = new List<Sprite>();
    private Canvas canvas;

    private void Awake()
    {
        if (dustSpriteSheet == null)
        {
            Debug.LogWarning($"{nameof(UIDustSpawner)} needs a dust sprite sheet.", this);
            enabled = false;
            return;
        }

        canvas = GetComponentInParent<Canvas>();
        CreateDustSprites();
        CreateDustLayer();

        for (int i = 0; i < dustCount; i++)
            motes.Add(CreateMote());
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        Rect bounds = dustLayer.rect;

        foreach (DustMote mote in motes)
        {
            Vector2 position = mote.rectTransform.anchoredPosition;
            position.y -= mote.fallSpeed * deltaTime;
            position.x += Mathf.Sin(Time.unscaledTime * mote.wobbleSpeed + mote.phase) * mote.drift * deltaTime;

            if (position.y < bounds.yMin - respawnPadding)
                ResetMote(mote, true);
            else
            {
                mote.rectTransform.anchoredPosition = position;
                SetMoteVisibility(mote, !OverlapsMagicCircle(mote, position));
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Sprite dustSprite in dustSprites)
            Destroy(dustSprite);
    }

    private void CreateDustLayer()
    {
        GameObject layerObject = new GameObject("Ambient Dust", typeof(RectTransform));
        dustLayer = layerObject.GetComponent<RectTransform>();
        dustLayer.SetParent(transform, false);
        dustLayer.anchorMin = Vector2.zero;
        dustLayer.anchorMax = Vector2.one;
        dustLayer.offsetMin = Vector2.zero;
        dustLayer.offsetMax = Vector2.zero;
        dustLayer.SetSiblingIndex(Mathf.Min(canvasSiblingIndex, transform.childCount - 1));
    }

    private DustMote CreateMote()
    {
        GameObject moteObject = new GameObject("Dust", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform moteTransform = moteObject.GetComponent<RectTransform>();
        moteTransform.SetParent(dustLayer, false);

        Image image = moteObject.GetComponent<Image>();
        image.sprite = dustSprites[Random.Range(0, dustSprites.Count)];
        image.raycastTarget = false;

        DustMote mote = new DustMote
        {
            rectTransform = moteTransform,
            image = image,
            fallSpeed = Random.Range(fallSpeedRange.x, fallSpeedRange.y),
            drift = Random.Range(sidewaysDriftRange.x, sidewaysDriftRange.y),
            wobbleSpeed = Random.Range(0.5f, 1.5f),
            phase = Random.Range(0f, Mathf.PI * 2f)
        };

        ResetMote(mote, false);
        return mote;
    }

    private void ResetMote(DustMote mote, bool fromTop)
    {
        Rect bounds = dustLayer.rect;
        float size = Random.Range(sizeRange.x, sizeRange.y);
        Color colour = dustColor;
        colour.a *= Random.Range(0.45f, 1f);

        mote.rectTransform.sizeDelta = Vector2.one * size;
        mote.rectTransform.anchoredPosition = new Vector2(
            Random.Range(bounds.xMin, bounds.xMax),
            fromTop ? bounds.yMax + respawnPadding : Random.Range(bounds.yMin, bounds.yMax));
        mote.baseColor = colour;
        SetMoteVisibility(mote, !OverlapsMagicCircle(mote, mote.rectTransform.anchoredPosition));
    }

    private bool OverlapsMagicCircle(DustMote mote, Vector2 motePosition)
    {
        if (magicCircle == null || canvas == null)
            return false;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 circleScreenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera,
            magicCircle.TransformPoint(magicCircle.rect.center));
        Vector2 circleEdgeScreenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera,
            magicCircle.TransformPoint(new Vector3(magicCircle.rect.xMax, magicCircle.rect.center.y)));

        RectTransformUtility.ScreenPointToLocalPointInRectangle(dustLayer, circleScreenPosition, uiCamera,
            out Vector2 circleCentre);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(dustLayer, circleEdgeScreenPosition, uiCamera,
            out Vector2 circleEdge);

        // Expand the circular exclusion by the sprite's half diagonal so no square corner can enter the circle.
        float moteHalfDiagonal = mote.rectTransform.sizeDelta.x * 0.707107f;
        float protectedRadius = Vector2.Distance(circleCentre, circleEdge) + moteHalfDiagonal;
        return Vector2.Distance(motePosition, circleCentre) < protectedRadius;
    }

    private static void SetMoteVisibility(DustMote mote, bool isVisible)
    {
        Color colour = mote.baseColor;
        if (!isVisible)
            colour.a = 0f;
        mote.image.color = colour;
    }

    private void CreateDustSprites()
    {
        float cellWidth = dustSpriteSheet.width / (float)spriteSheetColumns;
        float cellHeight = dustSpriteSheet.height / (float)spriteSheetRows;

        for (int row = 0; row < spriteSheetRows; row++)
        for (int column = 0; column < spriteSheetColumns; column++)
        {
            Rect cell = new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
            Sprite dustSprite = Sprite.Create(dustSpriteSheet, cell, new Vector2(0.5f, 0.5f), 100f);
            dustSprite.hideFlags = HideFlags.DontSave;
            dustSprites.Add(dustSprite);
        }
    }

    private class DustMote
    {
        public RectTransform rectTransform;
        public Image image;
        public float fallSpeed;
        public float drift;
        public float wobbleSpeed;
        public float phase;
        public Color baseColor;
    }
}
