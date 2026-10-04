using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class SettingsManager : MonoBehaviour
{
    [Serializable]
    public class SettingsData
    {
        public int settingsVersion = 3;
        public float masterVolume = 1f;
        public float musicVolume = 0.01f;
        public float sfxVolume = 1f;
        public float ambienceVolumeSlider = 1f;
        public float mouseSensitivity = 0.15f;
        public string interactKey = "E";
        public string useBinding = "MouseLeft";
        public bool vSync;
        public int qualityLevel = 1;
        // -1 means leave the project's configured startup display mode unchanged.
        public int fullscreenMode = -1;
    }

    public static SettingsData Current { get; private set; }
    public static string SettingsPath => Path.Combine(Application.persistentDataPath, "settings.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (FindFirstObjectByType<SettingsManager>() != null) return;
        new GameObject("SettingsManager").AddComponent<SettingsManager>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void Load()
    {
        TextAsset defaults = Resources.Load<TextAsset>("DefaultSettings");
        Current = defaults != null ? JsonUtility.FromJson<SettingsData>(defaults.text) : new SettingsData();
        if (File.Exists(SettingsPath))
        {
            string savedJson = File.ReadAllText(SettingsPath);
            bool isLegacySliderSave = !savedJson.Contains("\"settingsVersion\"");
            Current = JsonUtility.FromJson<SettingsData>(savedJson);

            // Older saves were created while the pixel sliders used a 0-100
            // integer range. Restore the intended defaults for those values.
            if (isLegacySliderSave || Current.settingsVersion < 3)
            {
                Current.musicVolume = 0.01f;
                Current.mouseSensitivity = 0.15f;
                Current.settingsVersion = 3;
            }
        }
        Current.masterVolume = Mathf.Clamp01(Current.masterVolume);
        Current.musicVolume = Mathf.Clamp01(Current.musicVolume);
        Current.sfxVolume = Mathf.Clamp01(Current.sfxVolume);
        Current.ambienceVolumeSlider = Mathf.Clamp01(Current.ambienceVolumeSlider);
        Current.mouseSensitivity = Mathf.Clamp(Current.mouseSensitivity, 0.01f, 0.5f);
        Save();
        Apply();
    }

    public static void Save()
    {
        if (Current != null) File.WriteAllText(SettingsPath, JsonUtility.ToJson(Current, true));
    }

    public static void Apply()
    {
        if (Current == null) return;
        AudioListener.volume = Mathf.Clamp01(Current.masterVolume);
        QualitySettings.SetQualityLevel(Mathf.Clamp(Current.qualityLevel, 0, QualitySettings.names.Length - 1), true);
        QualitySettings.vSyncCount = Current.vSync ? 1 : 0;
        if (Current.fullscreenMode >= 0 && Current.fullscreenMode <= (int)FullScreenMode.Windowed)
            Screen.fullScreenMode = (FullScreenMode)Current.fullscreenMode;
        if (AudioManager.Instance != null) {
            AudioManager.Instance.SetSfxMixerVolume(Current.sfxVolume);
            AudioManager.Instance.SetAmbienceMixerVolume(Current.ambienceVolumeSlider);

        }
    }

    public static bool InteractPressed() => Keyboard.current != null &&
        Enum.TryParse(Current.interactKey, out Key key) && Keyboard.current[key].wasPressedThisFrame;
    public static bool UsePressed() => Current.useBinding == "MouseLeft"
        ? Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
        : Keyboard.current != null && Enum.TryParse(Current.useBinding, out Key key) && Keyboard.current[key].wasPressedThisFrame;
}
