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

    [Header("Automatic rigidbody saves")]
    [SerializeField] private bool loadRigidbodyStateOnStart = true;
    [SerializeField] private bool autosaveWhenPaused = true;
    [SerializeField] private bool autosaveOnApplicationQuit = true;

    private const string RigidbodySaveFileName = "rigidbodies.save";
    private bool open;
    private string awaitingBinding;
    private string RigidbodySavePath => Path.Combine(Application.persistentDataPath, RigidbodySaveFileName);

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
        ShowPanel(mainPanel);
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        if (loadRigidbodyStateOnStart) LoadRigidbodies();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
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
            // UI/EventSystem processing can release the cursor after Escape was
            // handled, so apply the gameplay cursor state at the end of frame.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        Time.timeScale = 0f;
        if (pauseMenuRoot != null && !pauseMenuRoot.activeSelf)
            pauseMenuRoot.SetActive(true);
    }

    public void TogglePause() => SetPaused(!open);
    public void Pause() => SetPaused(true);
    public void Resume() => SetPaused(false);

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
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(open);
        PauseStateChanged?.Invoke(open);
    }

    public void OpenMain() => OpenPanel(mainPanel);
    public void OpenAudio() => OpenPanel(audioPanel);
    public void OpenControls() => OpenPanel(controlsPanel);
    public void OpenGraphics() => OpenPanel(graphicsPanel);
    public void QuitToDesktop() => Application.Quit();
    public void BeginInteractRebind() => BeginRebind("Interact");
    public void BeginUseRebind() => BeginRebind("Use");

    public void SetMasterVolume(float value) { SettingsManager.Current.masterVolume = Mathf.Clamp01(value); ApplyAndSaveSettings(); }
    public void SetMusicVolume(float value) { SettingsManager.Current.musicVolume = Mathf.Clamp01(value); ApplyAndSaveSettings(); }
    public void SetSfxVolume(float value) { SettingsManager.Current.sfxVolume = Mathf.Clamp01(value); ApplyAndSaveSettings(); }
    public void SetAmbienceVolume(float value) { SettingsManager.Current.ambienceVolumeSlider = Mathf.Clamp01(value); ApplyAndSaveSettings(); }
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
    }

    private static void ConfigureSlider(Slider slider, float min, float max)
    {
        if (slider == null)
            return;

        slider.wholeNumbers = false;
        slider.minValue = min;
        slider.maxValue = max;
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
        // MainPanel is the persistent left-hand navigation window, not a page.
        // Keep it visible while a settings panel is open on the right.
        if (mainPanel != null) mainPanel.SetActive(true);
        if (audioPanel != null) audioPanel.SetActive(panel == audioPanel);
        if (controlsPanel != null) controlsPanel.SetActive(panel == controlsPanel);
        if (graphicsPanel != null) graphicsPanel.SetActive(panel == graphicsPanel);
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
            if (awaitingBinding == "Interact") SettingsManager.Current.interactKey = key.keyCode.ToString();
            else SettingsManager.Current.useBinding = key.keyCode.ToString();
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
