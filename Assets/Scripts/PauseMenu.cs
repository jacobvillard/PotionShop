using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>Scene-owned pause/settings controller. Assign your UI in the Inspector.</summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    public static event Action<bool> PauseStateChanged;

    [Header("Menu roots")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject graphicsPanel;

    [Tooltip("Optional popups/windows that should be closed whenever the pause menu closes.")]
    [SerializeField] private List<GameObject> nonMenuWindows = new List<GameObject>();

    [Header("Audio controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider ambienceVolumeSlider;

    [Header("Control controls")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_Text interactBindingLabel;
    [SerializeField] private TMP_Text useBindingLabel;

    [Header("Graphics controls")]
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown fullscreenModeDropdown;

    [Header("Buttons - optional")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button audioButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button graphicsButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button interactRebindButton;
    [SerializeField] private Button useRebindButton;
    [SerializeField] private Button quitButton;

    [Header("Grimoire presentation")]
    [SerializeField] private TMP_FontAsset grimoireFont;
    [SerializeField] private Sprite grimoireWindow;
    [SerializeField] private Sprite grimoireButtonNormal;
    [SerializeField] private Sprite grimoireButtonHover;
    [SerializeField] private Sprite grimoireButtonPressed;
    [SerializeField] private Sprite grimoireSliderTrack;
    [SerializeField] private Sprite grimoireSliderFill;
    [SerializeField] private Sprite grimoireSliderKnob;

    [Header("Automatic rigidbody saves")]
    [SerializeField] private bool loadRigidbodyStateOnStart = true;
    [SerializeField] private bool autosaveWhenPaused = true;
    [SerializeField] private bool autosaveOnApplicationQuit = true;

    private const string RigidbodySaveFileName = "rigidbodies.save";
    private bool open;
    private bool externalMenuCursorActive;
    private string awaitingBinding;
    private PauseMenuIllustratedView illustratedView;
    private static string RigidbodySavePath => Path.Combine(Application.persistentDataPath, RigidbodySaveFileName);

    public static bool HasRigidbodySave() => File.Exists(RigidbodySavePath);

    public static void ClearRigidbodySave()
    {
        if (File.Exists(RigidbodySavePath))
            File.Delete(RigidbodySavePath);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshUiFromSettings();
        // Configure initial slider ranges and values before subscribing, so the
        // prefab's old 0-100 default cannot overwrite loaded settings.
        WireUiEvents();
        ApplyGrimoirePresentation();
        ShowPanel(mainPanel);
        illustratedView = GetComponent<PauseMenuIllustratedView>();
        if (illustratedView == null) illustratedView = gameObject.AddComponent<PauseMenuIllustratedView>();
        illustratedView.Build(this, pauseMenuRoot);
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        if (loadRigidbodyStateOnStart) LoadRigidbodies();
    }

    private void Update()
    {
        if (!externalMenuCursorActive && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
            return;
        }
        if (awaitingBinding != null) CaptureBinding();
    }

    private void LateUpdate()
    {
        // A navigation button or a UI transition must never unpause or hide the
        // pause root while this controller still considers the menu open.
        if (!open)
        {
            if (!externalMenuCursorActive)
            {
                // UI/EventSystem processing can release the cursor after Escape was
                // handled, so apply the gameplay cursor state at the end of frame.
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            return;
        }

        Time.timeScale = 0f;
        if (pauseMenuRoot != null && !pauseMenuRoot.activeSelf)
            pauseMenuRoot.SetActive(true);
    }

    public void TogglePause() => SetPaused(!open);
    public void Pause() => SetPaused(true);
    public void Resume() => SetPaused(false);

    /// <summary>
    /// Keeps the gameplay cursor released for full-screen menus (such as the Potiondex)
    /// without opening the pause-menu UI or changing time scale.
    /// </summary>
    public void SetExternalMenuCursor(bool active)
    {
        externalMenuCursorActive = active;
        // The Potiondex is a full-screen menu: freeze gameplay without showing
        // the pause root. On close, only resume if PauseMenu itself is closed.
        Time.timeScale = active || open ? 0f : 1f;
        MainMenuCursor menuCursor = GetComponent<MainMenuCursor>();
        if (menuCursor != null) menuCursor.SetCursorActive(active || open);
        else { Cursor.lockState = active || open ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = active || open; }
    }

    public void SetPaused(bool paused)
    {
        if (open == paused) return;
        if (paused && autosaveWhenPaused) SaveRigidbodies();

        open = paused;
        if (!open)
        {
            awaitingBinding = null;
            CloseNonMenuWindows();
            ShowPanel(mainPanel);
            UpdateBindingLabels();
        }
        Time.timeScale = open ? 0f : 1f;
        MainMenuCursor menuCursor = GetComponent<MainMenuCursor>();
        if (menuCursor != null) menuCursor.SetCursorActive(open || externalMenuCursorActive);
        else { Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = open; }
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(open);
        PauseStateChanged?.Invoke(open);
    }

    public void OpenMain() => OpenPanel(mainPanel);
    public void OpenAudio() => OpenPanel(audioPanel);
    public void OpenControls() => OpenPanel(controlsPanel);
    public void OpenGraphics() => OpenPanel(graphicsPanel);
    public void QuitToDesktop() => Application.Quit();
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
    public string InteractBinding => SettingsManager.Current != null ? SettingsManager.Current.interactKey : "E";
    public string UseBinding => SettingsManager.Current != null ? SettingsManager.Current.useBinding : "MouseLeft";
    public bool IsAwaitingBinding(string binding) => awaitingBinding == binding;
    public TMP_FontAsset GrimoireFont => grimoireFont;
    public Sprite GrimoireWindow => grimoireWindow;
    public Sprite GrimoireButtonNormal => grimoireButtonNormal;
    public Sprite GrimoireButtonHover => grimoireButtonHover;
    public Sprite GrimoireButtonPressed => grimoireButtonPressed;
    public Sprite GrimoireSliderTrack => grimoireSliderTrack;
    public Sprite GrimoireSliderFill => grimoireSliderFill;
    public Sprite GrimoireSliderKnob => grimoireSliderKnob;
    public void BeginInteractRebind() => BeginRebind("Interact");
    public void BeginUseRebind() => BeginRebind("Use");
    public void ResetControlsToDefault()
    {
        SettingsManager.Current.interactKey = "E";
        SettingsManager.Current.useBinding = "MouseLeft";
        awaitingBinding = null;
        ApplyAndSaveSettings();
        UpdateBindingLabels();
    }

    public void SetMasterVolume(float value) { SettingsManager.Current.masterVolume = Mathf.Clamp01(value); ApplyAndSaveSettings(); RefreshAudioValueLabels(); }
    public void SetMusicVolume(float value) { SettingsManager.Current.musicVolume = Mathf.Clamp01(value); ApplyAndSaveSettings(); RefreshAudioValueLabels(); }
    public void SetSfxVolume(float value) { SettingsManager.Current.sfxVolume = Mathf.Clamp01(value); ApplyAndSaveSettings(); RefreshAudioValueLabels(); }
    public void SetAmbienceVolume(float value) { SettingsManager.Current.ambienceVolumeSlider = Mathf.Clamp01(value); ApplyAndSaveSettings(); RefreshAudioValueLabels(); }
    public void SetMouseSensitivity(float value) { SettingsManager.Current.mouseSensitivity = Mathf.Clamp(value, 0.01f, 0.5f); ApplyAndSaveSettings(); }
    public void SetVSync(bool value) { SettingsManager.Current.vSync = value; ApplyAndSaveSettings(); }
    public void SetQualityLevel(int value) { SettingsManager.Current.qualityLevel = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1); ApplyAndSaveSettings(); }
    public void SetFullscreen(bool value) => SetFullscreenMode(value ? 1 : 0);
    public void SetFullscreenMode(int option)
    {
        // Dropdown order: Windowed, Borderless Fullscreen, Exclusive Fullscreen.
        SettingsManager.Current.fullscreenMode = option switch
        {
            1 => (int)FullScreenMode.FullScreenWindow,
            2 => (int)FullScreenMode.ExclusiveFullScreen,
            _ => (int)FullScreenMode.Windowed
        };
        ApplyAndSaveSettings();
        RefreshDisplayUi();
    }

    private void WireUiEvents()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (audioButton != null) audioButton.onClick.AddListener(OpenAudio);
        if (controlsButton != null) controlsButton.onClick.AddListener(OpenControls);
        if (graphicsButton != null) graphicsButton.onClick.AddListener(OpenGraphics);
        if (backButton != null) backButton.onClick.AddListener(OpenMain);
        if (interactRebindButton != null) interactRebindButton.onClick.AddListener(BeginInteractRebind);
        if (useRebindButton != null) useRebindButton.onClick.AddListener(BeginUseRebind);
        if (quitButton != null) quitButton.onClick.AddListener(QuitToDesktop);
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        if (ambienceVolumeSlider != null) ambienceVolumeSlider.onValueChanged.AddListener(SetAmbienceVolume);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
        if (vSyncToggle != null) vSyncToggle.onValueChanged.AddListener(SetVSync);
        if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(SetQualityLevel);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (fullscreenModeDropdown != null) fullscreenModeDropdown.onValueChanged.AddListener(SetFullscreenMode);
    }

    private void RefreshUiFromSettings()
    {
        if (SettingsManager.Current == null) return;
        ConfigureSlider(masterVolumeSlider, 0f, 1f);
        ConfigureSlider(musicVolumeSlider, 0f, 1f);
        ConfigureSlider(sfxVolumeSlider, 0f, 1f);
        ConfigureSlider(ambienceVolumeSlider, 0f, 1f);
        ConfigureSlider(mouseSensitivitySlider, 0.01f, 0.5f);
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(SettingsManager.Current.masterVolume);
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(SettingsManager.Current.musicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(SettingsManager.Current.sfxVolume);
        if (ambienceVolumeSlider != null) ambienceVolumeSlider.SetValueWithoutNotify(SettingsManager.Current.ambienceVolumeSlider);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(SettingsManager.Current.mouseSensitivity);
        if (vSyncToggle != null) vSyncToggle.SetIsOnWithoutNotify(SettingsManager.Current.vSync);
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(SettingsManager.Current.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1)));
        }
        RefreshDisplayUi();
        UpdateBindingLabels();
        RefreshAudioValueLabels();
    }

    private static void ConfigureSlider(Slider slider, float min, float max)
    {
        if (slider == null)
            return;

        slider.wholeNumbers = false;
        slider.minValue = min;
        slider.maxValue = max;
    }

    private void ApplyGrimoirePresentation()
    {
        if (pauseMenuRoot == null)
            return;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // The settings view is composed from the project's UI sprites at runtime;
        // no baked screenshot or generated UI backdrop is used.
        BuildIllustratedLayout();

        foreach (TMP_Text text in pauseMenuRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (grimoireFont != null) text.font = grimoireFont;
            bool heading = text.text.IndexOf("settings", StringComparison.OrdinalIgnoreCase) >= 0 || text.text.Equals("menu", StringComparison.OrdinalIgnoreCase);
            text.color = heading ? new Color(0.98f, 0.78f, 0.38f, 1f) : new Color(0.97f, 0.93f, 0.84f, 1f);
        }

        foreach (Button button in pauseMenuRoot.GetComponentsInChildren<Button>(true))
        {
            Image image = button.GetComponent<Image>();
            if (image == null || grimoireButtonNormal == null) continue;
            image.sprite = grimoireButtonNormal;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = grimoireButtonHover, pressedSprite = grimoireButtonPressed, selectedSprite = grimoireButtonHover, disabledSprite = grimoireButtonNormal };
        }

        foreach (Slider slider in pauseMenuRoot.GetComponentsInChildren<Slider>(true))
        {
            Image track = slider.targetGraphic as Image;
            if (track != null && grimoireSliderTrack != null) track.sprite = grimoireSliderTrack;
            Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (fill != null && grimoireSliderFill != null) fill.sprite = grimoireSliderFill;
            Image knob = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (knob != null && grimoireSliderKnob != null) knob.sprite = grimoireSliderKnob;
        }

        foreach (Image image in pauseMenuRoot.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "GrimoirePauseArtwork") continue;
            if (image.GetComponentInParent<Button>() != null || image.GetComponentInParent<Slider>() != null || image.GetComponentInParent<TMP_Dropdown>() != null || image.GetComponentInParent<Toggle>() != null) continue;
            if (grimoireWindow != null) image.sprite = grimoireWindow;
        }
    }

    private void BuildIllustratedLayout()
    {
        RectTransform root = pauseMenuRoot.GetComponent<RectTransform>();
        if (root == null)
            return;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        Transform existingArtwork = root.Find("GrimoirePauseArtwork");
        if (existingArtwork != null)
            existingArtwork.gameObject.SetActive(false);

        // The legacy Window prefabs use a six-times root scale. Keep that scale
        // so their existing child controls remain the intended readable size, but
        // place them inside the illustrated left/right pages.
        PlaceLivePanel(mainPanel, new Vector2(-505f, 0f), new Vector2(60f, 128f));
        PlaceLivePanel(audioPanel, new Vector2(245f, 0f), new Vector2(166f, 128f));
        PlaceLivePanel(controlsPanel, new Vector2(245f, 0f), new Vector2(166f, 128f));
        PlaceLivePanel(graphicsPanel, new Vector2(245f, 0f), new Vector2(166f, 128f));
    }

    private static void PlaceLivePanel(GameObject panel, Vector2 position, Vector2 logicalSize)
    {
        if (panel == null)
            return;

        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = logicalSize;
        rect.localScale = Vector3.one * 6f;

        // The panel sprite belongs to the replaced Wood UI. Hide it while
        // retaining its layout groups and all child controls.
        Image oldWindow = panel.GetComponent<Image>();
        if (oldWindow != null)
            oldWindow.color = new Color(1f, 1f, 1f, 0f);
    }

    private void RefreshAudioValueLabels()
    {
        if (pauseMenuRoot == null || SettingsManager.Current == null) return;
        SetAudioLabel("Main Volume", SettingsManager.Current.masterVolume);
        SetAudioLabel("Music Volume", SettingsManager.Current.musicVolume);
        SetAudioLabel("SFX Volume", SettingsManager.Current.sfxVolume);
        SetAudioLabel("Ambience Volume", SettingsManager.Current.ambienceVolumeSlider);
    }

    private void SetAudioLabel(string label, float value)
    {
        foreach (TMP_Text text in pauseMenuRoot.GetComponentsInChildren<TMP_Text>(true))
            if (text.text.StartsWith(label, StringComparison.OrdinalIgnoreCase)) { text.text = $"{label}   {Mathf.RoundToInt(value * 100f)}%"; return; }
    }


    private void RefreshDisplayUi()
    {
        FullScreenMode mode = SettingsManager.Current.fullscreenMode < 0
            ? Screen.fullScreenMode
            : (FullScreenMode)SettingsManager.Current.fullscreenMode;
        bool fullscreen = mode != FullScreenMode.Windowed;
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        if (fullscreenModeDropdown != null)
        {
            fullscreenModeDropdown.ClearOptions();
            fullscreenModeDropdown.AddOptions(new List<string> { "Windowed", "Borderless Fullscreen", "Exclusive Fullscreen" });
            int option = mode == FullScreenMode.ExclusiveFullScreen ? 2 : fullscreen ? 1 : 0;
            fullscreenModeDropdown.SetValueWithoutNotify(option);
        }
    }

    private void ShowPanel(GameObject panel)
    {
        if (illustratedView != null)
        {
            illustratedView.ShowPage(panel == audioPanel ? PauseMenuIllustratedView.Page.Audio : panel == controlsPanel ? PauseMenuIllustratedView.Page.Controls : panel == graphicsPanel ? PauseMenuIllustratedView.Page.Graphics : PauseMenuIllustratedView.Page.Main);
            return;
        }
        // MainPanel is the persistent left-hand navigation window, not a page.
        // Keep it visible while a settings panel is open on the right.
        if (mainPanel != null) mainPanel.SetActive(true);
        if (audioPanel != null) audioPanel.SetActive(panel == audioPanel);
        if (controlsPanel != null) controlsPanel.SetActive(panel == controlsPanel);
        if (graphicsPanel != null) graphicsPanel.SetActive(panel == graphicsPanel);
        SetActiveNavigationButton(panel == audioPanel ? audioButton : panel == controlsPanel ? controlsButton : panel == graphicsPanel ? graphicsButton : null);
    }

    private void SetActiveNavigationButton(Button activeButton)
    {
        foreach (Button button in new[] { audioButton, controlsButton, graphicsButton })
        {
            if (button == null)
                continue;
            Image image = button.GetComponent<Image>();
            if (image != null && grimoireButtonNormal != null)
                image.sprite = button == activeButton && grimoireButtonHover != null ? grimoireButtonHover : grimoireButtonNormal;
        }
    }

    private void OpenPanel(GameObject panel)
    {
        // Navigation buttons never resume the game: they keep the pause menu open
        // and only change which settings panel is visible.
        if (!open)
            SetPaused(true);
        else if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(true);
        ShowPanel(panel);
    }

    public void CloseNonMenuWindows()
    {
        foreach (GameObject window in nonMenuWindows)
            if (window != null)
                window.SetActive(false);
    }

    private void BeginRebind(string binding) { awaitingBinding = binding; UpdateBindingLabels(); }

    private void CaptureBinding()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            awaitingBinding = null;
            UpdateBindingLabels();
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && awaitingBinding == "Use")
        {
            SettingsManager.Current.useBinding = "MouseLeft";
            CompleteRebind();
            return;
        }
        if (Keyboard.current == null) return;

        foreach (KeyControl key in Keyboard.current.allKeys)
        {
            if (!key.wasPressedThisFrame) continue;
            string binding = key.keyCode.ToString();
            if (awaitingBinding == "Interact" && binding == SettingsManager.Current.useBinding) return;
            if (awaitingBinding == "Use" && binding == SettingsManager.Current.interactKey) return;
            if (awaitingBinding == "Interact") SettingsManager.Current.interactKey = binding;
            else SettingsManager.Current.useBinding = binding;
            CompleteRebind();
            return;
        }
    }

    private void CompleteRebind() { awaitingBinding = null; ApplyAndSaveSettings(); UpdateBindingLabels(); }

    private void UpdateBindingLabels()
    {
        if (interactBindingLabel != null) interactBindingLabel.text = awaitingBinding == "Interact" ? "Press a key..." : SettingsManager.Current.interactKey;
        if (useBindingLabel != null) useBindingLabel.text = awaitingBinding == "Use" ? "Press a key..." : SettingsManager.Current.useBinding;
    }

    private static void ApplyAndSaveSettings() { SettingsManager.Apply(); SettingsManager.Save(); }

    private void SaveRigidbodies()
    {
        RigidbodySaveFile save = new RigidbodySaveFile();
        foreach (Rigidbody body in FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            save.bodies.Add(new RigidbodyState
            {
                sceneName = body.gameObject.scene.name,
                hierarchyPath = GetHierarchyPath(body.transform),
                position = body.position,
                rotation = body.rotation,
                velocity = body.linearVelocity,
                angularVelocity = body.angularVelocity,
                isKinematic = body.isKinematic,
                useGravity = body.useGravity
            });
        }
        File.WriteAllText(RigidbodySavePath, JsonUtility.ToJson(save, true));
    }

    private void LoadRigidbodies()
    {
        if (!File.Exists(RigidbodySavePath)) return;
        RigidbodySaveFile save = JsonUtility.FromJson<RigidbodySaveFile>(File.ReadAllText(RigidbodySavePath));
        if (save?.bodies == null) return;

        foreach (RigidbodyState state in save.bodies)
        {
            Transform target = FindTransform(state.sceneName, state.hierarchyPath);
            Rigidbody body = target != null ? target.GetComponent<Rigidbody>() : null;
            if (body == null) continue;
            body.isKinematic = state.isKinematic;
            body.useGravity = state.useGravity;
            body.position = state.position;
            body.rotation = state.rotation;
            body.linearVelocity = state.velocity;
            body.angularVelocity = state.angularVelocity;
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        List<int> indices = new List<int>();
        for (Transform current = transform; current != null; current = current.parent) indices.Add(current.GetSiblingIndex());
        indices.Reverse();
        return string.Join("/", indices);
    }

    private static Transform FindTransform(string sceneName, string hierarchyPath)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid()) return null;
        string[] indices = hierarchyPath.Split('/');
        if (indices.Length == 0 || !int.TryParse(indices[0], out int rootIndex)) return null;
        GameObject[] roots = scene.GetRootGameObjects();
        if (rootIndex < 0 || rootIndex >= roots.Length) return null;

        Transform current = roots[rootIndex].transform;
        for (int i = 1; i < indices.Length; i++)
        {
            if (!int.TryParse(indices[i], out int childIndex) || childIndex < 0 || childIndex >= current.childCount) return null;
            current = current.GetChild(childIndex);
        }
        return current;
    }

    private void OnApplicationQuit() { if (autosaveOnApplicationQuit) SaveRigidbodies(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    [Serializable] private class RigidbodySaveFile { public List<RigidbodyState> bodies = new List<RigidbodyState>(); }
    [Serializable] private class RigidbodyState
    {
        public string sceneName;
        public string hierarchyPath;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public bool isKinematic;
        public bool useGravity;
    }
}
