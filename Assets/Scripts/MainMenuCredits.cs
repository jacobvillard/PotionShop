using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the third-party acknowledgements as a cinematic, automatically scrolling
/// credits page. The source lives at Resources/Credits/THIRD_PARTY_CREDITS.txt.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuCredits : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.35f;
    [SerializeField, Min(5f)] private float scrollDuration = 42f;
    [Header("Menu elements to fade")]
    [Tooltip("Drag any title-menu objects to fade here. Objects not in this list stay visible during credits.")]
    [SerializeField] private Transform[] fadeTargets;

    private CanvasGroup[] menuGroups;
    private CanvasGroup creditsGroup;
    private RectTransform scrollText;
    private bool showing;
    private float scrollElapsed;
    private Coroutine transition;

    private void Awake()
    {
        Button creditsButton = FindCreditsButton();
        if (creditsButton == null)
        {
            Debug.LogWarning("MainMenuCredits could not find a button labelled Credits.", this);
            enabled = false;
            return;
        }

        creditsButton.onClick.AddListener(ShowCredits);
        CreateCreditsPage();
        CacheMenuGroups();
    }

    private Button FindCreditsButton()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.Trim().Equals("Credits", System.StringComparison.OrdinalIgnoreCase))
                return button;
        }

        return null;
    }

    private void CacheMenuGroups()
    {
        var groups = new System.Collections.Generic.List<CanvasGroup>();
        if (fadeTargets == null)
            fadeTargets = System.Array.Empty<Transform>();

        foreach (Transform child in fadeTargets)
        {
            if (child == null)
                continue;

            CanvasGroup group = child.GetComponent<CanvasGroup>();
            if (group == null)
                group = child.gameObject.AddComponent<CanvasGroup>();
            groups.Add(group);
        }
        menuGroups = groups.ToArray();
    }

    private void CreateCreditsPage()
    {
        GameObject page = CreateUiObject("Credits Page", transform);
        RectTransform pageRect = page.GetComponent<RectTransform>();
        Stretch(pageRect);
        page.transform.SetAsLastSibling();
        creditsGroup = page.AddComponent<CanvasGroup>();
        creditsGroup.alpha = 0f;
        creditsGroup.interactable = false;
        creditsGroup.blocksRaycasts = false;

        TMP_FontAsset font = FindAnyObjectByType<TMP_Text>()?.font;
        CreateLabel("Credits Title", page.transform, "THIRD-PARTY CREDITS", font, 42f, FontStyles.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(1100f, 80f), TextAlignmentOptions.Center);

        GameObject viewport = CreateUiObject("Credits Viewport", page.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0f, -12f);
        viewportRect.sizeDelta = new Vector2(1150f, 760f);
        viewport.AddComponent<RectMask2D>();

        TMP_Text body = CreateLabel("Credits Scroll", viewport.transform, FormatCredits(), font, 25f, FontStyles.Normal,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1080f, 0f), TextAlignmentOptions.Center);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        body.lineSpacing = 7f;
        body.ForceMeshUpdate();
        scrollText = body.rectTransform;
        float contentHeight = Mathf.Max(1000f, body.preferredHeight + 220f);
        scrollText.sizeDelta = new Vector2(1080f, contentHeight);
        scrollText.anchoredPosition = new Vector2(0f, -contentHeight * 0.5f);

        Button exit = CreateExitButton(page.transform, font);
        exit.onClick.AddListener(HideCredits);
    }

    private Button CreateExitButton(Transform parent, TMP_FontAsset font)
    {
        GameObject buttonObject = CreateUiObject("Exit Credits", parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 46f);
        rect.sizeDelta = new Vector2(330f, 58f);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.34f, 0.22f, 0.1f, 0.92f);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.91f, 0.7f, 1f);
        colors.pressedColor = new Color(0.75f, 0.62f, 0.42f, 1f);
        button.colors = colors;
        CreateLabel("Label", buttonObject.transform, "EXIT CREDITS", font, 24f, FontStyles.Bold,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        return button;
    }

    public void ShowCredits()
    {
        if (showing) return;
        showing = true;
        scrollElapsed = 0f;
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(Transition(true));
    }

    public void HideCredits()
    {
        if (!showing) return;
        showing = false;
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(Transition(false));
    }

    private IEnumerator Transition(bool entering)
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            SetMenuAlpha(entering ? 1f - t : t);
            creditsGroup.alpha = entering ? t : 1f - t;
            yield return null;
        }
        SetMenuAlpha(entering ? 0f : 1f);
        creditsGroup.alpha = entering ? 1f : 0f;
        creditsGroup.interactable = entering;
        creditsGroup.blocksRaycasts = entering;
        transition = null;
    }

    private void Update()
    {
        if (!showing || scrollText == null || transition != null) return;
        float contentHeight = scrollText.rect.height;
        float travel = contentHeight + 760f;
        scrollElapsed += Time.unscaledDeltaTime;
        float offset = Mathf.Repeat(scrollElapsed / scrollDuration, 1f) * travel;
        scrollText.anchoredPosition = new Vector2(0f, -contentHeight * 0.5f + offset);
    }

    private void SetMenuAlpha(float alpha)
    {
        foreach (CanvasGroup group in menuGroups)
        {
            group.alpha = alpha;
            group.interactable = alpha > 0.99f;
            group.blocksRaycasts = alpha > 0.99f;
        }
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, TMP_FontAsset font, float size, FontStyles style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment)
    {
        GameObject labelObject = CreateUiObject(name, parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = new Color(1f, 0.88f, 0.62f, 1f);
        label.raycastTarget = false;
        return label;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        result.layer = parent.gameObject.layer;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static string FormatCredits()
    {
        TextAsset source = Resources.Load<TextAsset>("Credits/THIRD_PARTY_CREDITS");
        if (source == null)
            return "<size=34><b>Credits file missing</b></size>\n\nAdd THIRD_PARTY_CREDITS.txt to Resources/Credits.";

        StringBuilder formatted = new StringBuilder();
        foreach (string rawLine in source.text.Replace("\r", string.Empty).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("# ")) formatted.Append("<size=42><b>").Append(line.Substring(2)).Append("</b></size>\n\n");
            else if (line.StartsWith("## ")) formatted.Append("<size=32><b>").Append(line.Substring(3)).Append("</b></size>\n\n");
            else if (line.StartsWith("### ")) formatted.Append("<size=28><b>").Append(line.Substring(4)).Append("</b></size>\n");
            else if (line.StartsWith("- ")) formatted.Append("• ").Append(StripMarkdown(line.Substring(2))).Append("\n\n");
            else if (!string.IsNullOrEmpty(line)) formatted.Append(StripMarkdown(line)).Append("\n\n");
        }
        return formatted.ToString();
    }

    private static string StripMarkdown(string text) => text.Replace("**", string.Empty).Replace("[", string.Empty).Replace("](", " — ").Replace(")", string.Empty);
}
