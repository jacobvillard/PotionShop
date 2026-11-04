using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the pause menu from live Unity UI primitives. It owns presentation only;
/// PauseMenu remains the source of truth for pause state and saved settings.
/// </summary>
public sealed class PauseMenuIllustratedView : MonoBehaviour
{
    public enum Page { Main, Audio, Controls, Graphics }

    private static readonly Color Ink = new Color(0.075f, 0.035f, 0.11f, 0.94f);
    private static readonly Color Paper = new Color(0.15f, 0.075f, 0.21f, 0.98f);
    private static readonly Color FieldColor = new Color(0.105f, 0.045f, 0.155f, 1f);
    private static readonly Color Lavender = new Color(0.63f, 0.34f, 0.82f, 1f);
    private static readonly Color Gold = new Color(1f, 0.72f, 0.25f, 1f);
    private static readonly Color Ivory = new Color(1f, 0.94f, 0.82f, 1f);
    private static TMP_FontAsset activeFont;
    private static Sprite windowSprite;
    private static Sprite navigationSprite;
    private static Sprite buttonNormal;
    private static Sprite buttonHover;
    private static Sprite buttonPressed;
    private static Sprite sliderTrack;
    private static Sprite sliderFill;
    private static Sprite sliderKnob;

    private PauseMenu controller;
    private GameObject root;
    private GameObject content;
    private Page currentPage;
    private TMP_Text interactValue;
    private TMP_Text useValue;
    private TMP_Text sensitivityValue;
    private TMP_Text[] volumeValues = new TMP_Text[4];
    private Button[] navButtons;
    private Outline[] navBorders;
    private Toggle vsync;

    public void Build(PauseMenu pauseMenu, GameObject pauseRoot)
    {
        controller = pauseMenu;
        activeFont = controller != null && controller.GrimoireFont != null ? controller.GrimoireFont : TMP_Settings.defaultFontAsset;
        windowSprite = Resources.Load<Sprite>("PauseUI/SettingsPanelFrame") ?? (controller != null ? controller.GrimoireWindow : null);
        navigationSprite = Resources.Load<Sprite>("PauseUI/NavigationPanelFrame") ?? windowSprite;
        buttonNormal = Resources.Load<Sprite>("PauseUI/ButtonNormal") ?? (controller != null ? controller.GrimoireButtonNormal : null);
        buttonHover = Resources.Load<Sprite>("PauseUI/ButtonSelected") ?? (controller != null ? controller.GrimoireButtonHover : null);
        buttonPressed = buttonHover ?? (controller != null ? controller.GrimoireButtonPressed : null);
        sliderTrack = controller != null ? controller.GrimoireSliderTrack : null;
        sliderFill = controller != null ? controller.GrimoireSliderFill : null;
        sliderKnob = controller != null ? controller.GrimoireSliderKnob : null;
        if (pauseRoot == null || root != null)
            return;

        // Do not delete the old setup: it still contains the controller's fields.
        // Hide it and draw a deliberately composed view over the illustrated frame.
        foreach (Transform child in pauseRoot.transform)
            if (child.name != "GrimoirePauseArtwork") child.gameObject.SetActive(false);

        root = new GameObject("IllustratedPauseControls", typeof(RectTransform));
        root.transform.SetParent(pauseRoot.transform, false);
        Stretch(root.GetComponent<RectTransform>(), new Vector2(0.09f, 0.09f), new Vector2(0.91f, 0.91f));

        BuildNavigation();
        content = Panel("SettingsPage", root.transform, new Vector2(0.275f, 0.04f), new Vector2(0.985f, 0.96f), Color.clear, false);
        ShowPage(Page.Main);
    }

    public void ShowPage(Page page)
    {
        if (content == null) return;
        currentPage = page;
        Clear(content.transform);
        UpdateNavigation();
        switch (page)
        {
            case Page.Audio: BuildAudio(); break;
            case Page.Controls: BuildControls(); break;
            case Page.Graphics: BuildGraphics(); break;
            default: BuildMain(); break;
        }
    }

    private void BuildNavigation()
    {
        GameObject panel = Panel("Navigation", root.transform, new Vector2(0.015f, 0.04f), new Vector2(0.25f, 0.96f), Ink, true);
        Text("Menu", panel.transform, new Vector2(0.08f, 0.875f), new Vector2(0.92f, 0.97f), 38, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Divider(panel.transform, 0.85f);

        string[] labels = { "Resume", "Audio", "Controls", "Graphics", "Main Menu", "Quit Game" };
        Action[] actions =
        {
            controller.Resume,
            () => controller.OpenAudio(),
            () => controller.OpenControls(),
            () => controller.OpenGraphics(),
            controller.ReturnToMainMenu,
            controller.QuitToDesktop
        };
        navButtons = new Button[labels.Length];
        navBorders = new Outline[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            float top = 0.81f - i * 0.112f;
            navButtons[i] = Button(labels[i], panel.transform, new Vector2(0.09f, top - 0.08f), new Vector2(0.91f, top), actions[i], 26, out navBorders[i]);
        }
    }

    private void BuildMain()
    {
        Header("Pause Menu", "Select a category to adjust your settings.");
        Text("Your brew waits safely while the shop is paused.", content.transform, new Vector2(0.11f, 0.40f), new Vector2(0.89f, 0.52f), 30, Ivory, TextAlignmentOptions.Center);
        Text("Choose Audio, Controls, or Graphics from the menu on the left.", content.transform, new Vector2(0.11f, 0.31f), new Vector2(0.89f, 0.40f), 21, new Color(Ivory.r, Ivory.g, Ivory.b, .72f), TextAlignmentOptions.Center);
    }

    private void BuildAudio()
    {
        Header("Audio Settings", "Adjust the soundscape of your potion shop.");
        string[] names = { "Main Volume", "Music Volume", "SFX Volume", "Ambience Volume" };
        float[] values =
        {
            SettingsManager.Current.masterVolume,
            SettingsManager.Current.musicVolume,
            SettingsManager.Current.sfxVolume,
            SettingsManager.Current.ambienceVolumeSlider
        };
        Action<float>[] setters = { controller.SetMasterVolume, controller.SetMusicVolume, controller.SetSfxVolume, controller.SetAmbienceVolume };
        for (int i = 0; i < names.Length; i++)
        {
            float y = 0.68f - i * 0.14f;
            Text(names[i], content.transform, new Vector2(0.08f, y), new Vector2(0.36f, y + .07f), 23, Ivory);
            Slider slider = Slider(content.transform, new Vector2(.38f, y + .012f), new Vector2(.79f, y + .055f), values[i]);
            int index = i;
            slider.onValueChanged.AddListener(value => { setters[index](value); RefreshAudioValues(); });
            volumeValues[i] = Text($"{Mathf.RoundToInt(values[i] * 100)}%", content.transform, new Vector2(.82f, y), new Vector2(.93f, y + .07f), 22, Gold, TextAlignmentOptions.Right);
        }
    }

    private void BuildControls()
    {
        Header("Controls Settings", "Bindings are saved automatically.");
        Text("Mouse Sensitivity", content.transform, new Vector2(.08f, .67f), new Vector2(.39f, .74f), 23, Ivory);
        Slider sensitivity = Slider(content.transform, new Vector2(.08f, .61f), new Vector2(.72f, .65f), SettingsManager.Current.mouseSensitivity, .01f, .5f);
        sensitivityValue = Text(SettingsManager.Current.mouseSensitivity.ToString("0.00"), content.transform, new Vector2(.76f, .59f), new Vector2(.91f, .67f), 22, Gold, TextAlignmentOptions.Center, FontStyles.Bold, true);
        sensitivity.onValueChanged.AddListener(value => { controller.SetMouseSensitivity(value); sensitivityValue.text = value.ToString("0.00"); });
        Divider(content.transform, .53f);

        BindingRow("Interact Key", .40f, "Interact", controller.BeginInteractRebind, out interactValue);
        BindingRow("Use Key", .24f, "Use", controller.BeginUseRebind, out useValue);
        Button("Reset Controls to Default", content.transform, new Vector2(.56f, .08f), new Vector2(.91f, .16f), controller.ResetControlsToDefault, 19, out _);
    }

    private void BuildGraphics()
    {
        Header("Graphics Settings", "Choose a presentation that suits your workshop.");
        Text("Graphics Quality", content.transform, new Vector2(.08f, .68f), new Vector2(.42f, .75f), 23, Ivory);
        string[] qualities = { "Low", "Medium", "High", "Ultra" };
        for (int i = 0; i < qualities.Length; i++)
        {
            int level = Mathf.Min(i, Mathf.Max(0, QualitySettings.names.Length - 1));
            Button(qualities[i], content.transform, new Vector2(.08f + i * .18f, .58f), new Vector2(.245f + i * .18f, .65f), () => controller.SetQualityLevel(level), 18, out _);
        }
        Text("Display Mode", content.transform, new Vector2(.08f, .44f), new Vector2(.42f, .51f), 23, Ivory);
        string[] modes = { "Windowed", "Borderless", "Fullscreen" };
        for (int i = 0; i < modes.Length; i++)
        {
            int mode = i;
            Button(modes[i], content.transform, new Vector2(.08f + i * .25f, .34f), new Vector2(.305f + i * .25f, .41f), () => controller.SetFullscreenMode(mode), 18, out _);
        }
        Text("VSync", content.transform, new Vector2(.08f, .18f), new Vector2(.25f, .25f), 23, Ivory);
        vsync = Toggle(content.transform, new Vector2(.29f, .18f), new Vector2(.36f, .25f), SettingsManager.Current.vSync);
        vsync.onValueChanged.AddListener(controller.SetVSync);
    }

    private void BindingRow(string label, float y, string binding, Action action, out TMP_Text value)
    {
        Text(label, content.transform, new Vector2(.08f, y), new Vector2(.34f, y + .07f), 22, Ivory);
        value = Field(content.transform, new Vector2(.38f, y), new Vector2(.62f, y + .07f), binding == "Interact" ? controller.InteractBinding : controller.UseBinding);
        Button("Rebind", content.transform, new Vector2(.67f, y), new Vector2(.86f, y + .07f), action, 19, out _);
    }

    private void Header(string title, string subtitle)
    {
        Text(title, content.transform, new Vector2(.075f, .86f), new Vector2(.91f, .96f), 39, Gold, TextAlignmentOptions.Left, FontStyles.Bold);
        Divider(content.transform, .84f);
        Text(subtitle, content.transform, new Vector2(.08f, .78f), new Vector2(.91f, .83f), 17, new Color(Ivory.r, Ivory.g, Ivory.b, .72f));
    }

    private void Update()
    {
        if (root == null || !root.activeInHierarchy) return;
        if (interactValue != null) interactValue.text = controller.IsAwaitingBinding("Interact") ? "Press any key..." : controller.InteractBinding;
        if (useValue != null) useValue.text = controller.IsAwaitingBinding("Use") ? "Press any key..." : controller.UseBinding;
    }

    private void RefreshAudioValues()
    {
        if (SettingsManager.Current == null) return;
        float[] values = { SettingsManager.Current.masterVolume, SettingsManager.Current.musicVolume, SettingsManager.Current.sfxVolume, SettingsManager.Current.ambienceVolumeSlider };
        for (int i = 0; i < volumeValues.Length; i++) if (volumeValues[i] != null) volumeValues[i].text = $"{Mathf.RoundToInt(values[i] * 100)}%";
    }

    private void UpdateNavigation()
    {
        if (navBorders == null) return;
        for (int i = 0; i < navBorders.Length; i++)
        {
            bool active = (i == 1 && currentPage == Page.Audio) || (i == 2 && currentPage == Page.Controls) || (i == 3 && currentPage == Page.Graphics);
            navBorders[i].effectColor = active ? Gold : new Color(Lavender.r, Lavender.g, Lavender.b, .72f);
            navButtons[i].GetComponent<Image>().color = active ? new Color(.31f, .13f, .35f, 1f) : new Color(.12f, .055f, .17f, 1f);
        }
    }

    private static GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color, bool border)
    {
        GameObject go = Box(name, parent, min, max, color);
        Image image = go.GetComponent<Image>();
        Sprite panelSprite = name == "Navigation" ? navigationSprite : windowSprite;
        if (panelSprite != null)
        {
            image.sprite = panelSprite;
            image.type = name == "Navigation" ? Image.Type.Simple : Image.Type.Sliced;
            image.preserveAspect = name == "Navigation";
            image.color = Color.white;
        }
        if (border)
        {
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = Lavender;
            outline.effectDistance = new Vector2(3f, -3f);
        }
        return go;
    }

    private static Button Button(string label, Transform parent, Vector2 min, Vector2 max, Action action, int size, out Outline border)
    {
        GameObject outer = Box(label + "Button", parent, min, max, new Color(.12f, .055f, .17f, 1f));
        border = outer.AddComponent<Outline>();
        border.effectColor = new Color(Lavender.r, Lavender.g, Lavender.b, .72f);
        border.effectDistance = new Vector2(2f, -2f);
        Button button = outer.AddComponent<Button>();
        Image image = outer.GetComponent<Image>();
        if (buttonNormal != null)
        {
            image.sprite = buttonNormal;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = buttonHover != null ? buttonHover : buttonNormal,
                selectedSprite = buttonHover != null ? buttonHover : buttonNormal,
                pressedSprite = buttonPressed != null ? buttonPressed : buttonNormal,
                disabledSprite = buttonNormal
            };
        }
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .76f, .38f, 1f);
        colors.pressedColor = new Color(.65f, .36f, .82f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => action());
        Text(label, outer.transform, Vector2.zero, Vector2.one, size, Ivory, TextAlignmentOptions.Center, FontStyles.Bold);
        return button;
    }

    private static Slider Slider(Transform parent, Vector2 min, Vector2 max, float value, float low = 0f, float high = 1f)
    {
        GameObject host = Box("Slider", parent, min, max, new Color(.045f, .018f, .07f, 1f));
        if (sliderTrack != null)
        {
            host.GetComponent<Image>().sprite = sliderTrack;
            host.GetComponent<Image>().type = Image.Type.Sliced;
            host.GetComponent<Image>().color = Color.white;
        }
        Outline outline = host.AddComponent<Outline>(); outline.effectColor = Lavender; outline.effectDistance = new Vector2(1.5f, -1.5f);
        Slider slider = host.AddComponent<Slider>();
        slider.minValue = low; slider.maxValue = high; slider.value = value;
        GameObject fillArea = Box("Fill Area", host.transform, new Vector2(.02f, .2f), new Vector2(.98f, .8f), Color.clear);
        GameObject fill = Box("Fill", fillArea.transform, Vector2.zero, new Vector2(.5f, 1f), Lavender);
        GameObject handle = Box("Handle", host.transform, new Vector2(.46f, -.38f), new Vector2(.54f, 1.38f), Gold);
        if (sliderFill != null)
        {
            fill.GetComponent<Image>().sprite = sliderFill;
            fill.GetComponent<Image>().type = Image.Type.Sliced;
            fill.GetComponent<Image>().color = Color.white;
        }
        if (sliderKnob != null)
        {
            handle.GetComponent<Image>().sprite = sliderKnob;
            handle.GetComponent<Image>().preserveAspect = true;
            handle.GetComponent<Image>().color = Color.white;
        }
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        return slider;
    }

    private static Toggle Toggle(Transform parent, Vector2 min, Vector2 max, bool value)
    {
        GameObject host = Box("Toggle", parent, min, max, FieldColor);
        Outline outline = host.AddComponent<Outline>(); outline.effectColor = Gold; outline.effectDistance = new Vector2(2f, -2f);
        Toggle toggle = host.AddComponent<Toggle>();
        GameObject mark = Box("Checkmark", host.transform, new Vector2(.22f, .20f), new Vector2(.78f, .80f), Gold);
        toggle.graphic = mark.GetComponent<Image>();
        toggle.targetGraphic = host.GetComponent<Image>();
        toggle.isOn = value;
        return toggle;
    }

    private static TMP_Text Field(Transform parent, Vector2 min, Vector2 max, string value)
    {
        GameObject box = Box("Value Field", parent, min, max, FieldColor);
        Outline outline = box.AddComponent<Outline>(); outline.effectColor = Lavender; outline.effectDistance = new Vector2(2f, -2f);
        return Text(value, box.transform, Vector2.zero, Vector2.one, 20, Ivory, TextAlignmentOptions.Center);
    }

    private static TMP_Text Text(string value, Transform parent, Vector2 min, Vector2 max, int size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal, bool boxed = false)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); Stretch(rect, min, max);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = activeFont != null ? activeFont : TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    private static void Divider(Transform parent, float y)
    {
        Box("Gold Divider", parent, new Vector2(.07f, y), new Vector2(.93f, y + .006f), new Color(Gold.r, Gold.g, Gold.b, .8f));
    }

    private static GameObject Box(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>(), min, max);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
