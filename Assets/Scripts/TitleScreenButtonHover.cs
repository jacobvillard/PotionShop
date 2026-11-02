using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds a warm, responsive hover treatment to a title-screen UI button.
/// The side accents are created at runtime, so no extra UI objects are needed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class TitleScreenButtonHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Hover Style")]
    [SerializeField] private Color champagneTextColor = new Color(0.9529412f, 0.8470588f, 0.6352941f, 1f);
    [SerializeField, Range(1f, 1.15f)] private float hoverScale = 1.06f;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.14f;

    [Header("Side Reveal")]
    [SerializeField, Min(0f)] private float accentWidth = 42f;
    [SerializeField, Min(0f)] private float accentInset = 16f;
    [SerializeField, Min(1f)] private float accentHeight = 2f;

    [Header("Audio")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.65f;

    private RectTransform rectTransform;
    private Button button;
    private TMP_Text label;
    private Vector3 restingScale;
    private Color restingTextColor;
    private RectTransform leftAccent;
    private RectTransform rightAccent;
    private float hoverAmount;
    private float hoverVelocity;
    private bool isHovered;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        button = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>(true);
        restingScale = rectTransform.localScale;

        if (label != null)
            restingTextColor = label.color;

        button.transition = Selectable.Transition.None;
        leftAccent = CreateAccent("Hover Accent Left", 1f);
        rightAccent = CreateAccent("Hover Accent Right", 0f);
        RefreshAccentPositions();
        ConfigureAudio();

        if (GetComponent<TitleScreenButtonEntrance>() == null)
            gameObject.AddComponent<TitleScreenButtonEntrance>();

        ApplyVisuals(0f);
    }

    private void Update()
    {
        float target = isHovered && button.interactable ? 1f : 0f;
        hoverAmount = Mathf.SmoothDamp(
            hoverAmount,
            target,
            ref hoverVelocity,
            animationDuration,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        ApplyVisuals(hoverAmount);
    }

    public void OnPointerEnter(PointerEventData eventData) => isHovered = true;
    public void OnPointerExit(PointerEventData eventData) => isHovered = false;
    public void OnSelect(BaseEventData eventData) => isHovered = true;
    public void OnDeselect(BaseEventData eventData) => isHovered = false;

    private RectTransform CreateAccent(string accentName, float pivotX)
    {
        GameObject accentObject = new GameObject(accentName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform accent = accentObject.GetComponent<RectTransform>();
        accent.SetParent(rectTransform, false);
        accent.anchorMin = new Vector2(0.5f, 0.5f);
        accent.anchorMax = new Vector2(0.5f, 0.5f);
        accent.pivot = new Vector2(pivotX, 0.5f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(0f, accentHeight);
        accent.SetAsFirstSibling();

        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = champagneTextColor;
        accentImage.raycastTarget = false;
        return accent;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (leftAccent != null && rightAccent != null)
            RefreshAccentPositions();
    }

    private void RefreshAccentPositions()
    {
        if (label == null)
            return;

        label.ForceMeshUpdate();
        float maximumTextHalfWidth = Mathf.Max(
            0f,
            rectTransform.rect.width * 0.5f - accentWidth - accentInset);
        float textHalfWidth = Mathf.Min(label.preferredWidth * 0.5f, maximumTextHalfWidth);
        float offset = textHalfWidth + accentInset;
        leftAccent.anchoredPosition = new Vector2(-offset, 0f);
        rightAccent.anchoredPosition = new Vector2(offset, 0f);
    }

    private void ConfigureAudio()
    {
        if (hoverSound == null && clickSound == null)
            return;

        UIButtonAudio audio = GetComponent<UIButtonAudio>();
        if (audio == null)
            audio = gameObject.AddComponent<UIButtonAudio>();
        audio.Configure(hoverSound, clickSound, hoverVolume, clickVolume);
    }

    private void ApplyVisuals(float amount)
    {
        rectTransform.localScale = Vector3.Lerp(restingScale, restingScale * hoverScale, amount);

        if (label != null)
        {
            Color targetColor = champagneTextColor;
            targetColor.a = restingTextColor.a;
            label.color = Color.Lerp(restingTextColor, targetColor, amount);
        }

        SetAccentWidth(leftAccent, amount);
        SetAccentWidth(rightAccent, amount);
    }

    private void SetAccentWidth(RectTransform accent, float amount)
    {
        if (accent != null)
            accent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, accentWidth * amount);
    }
}
