using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Wires the title-screen navigation and protects an existing save from accidental replacement.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuActions : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "Scene";
    [SerializeField] private bool preloadGameplayScene = true;
    [SerializeField, Min(0.01f)] private float loadingFadeDuration = 0.2f;

    private Button continueButton;
    private GameObject overwritePrompt;
    private CanvasGroup loadingScreen;
    private AsyncOperation preloadOperation;
    private bool loadingGameplay;

    private void Awake()
    {
        continueButton = FindButton("CONTINUE GAME");
        Button newGameButton = FindButton("NEW GAME");
        Button quitButton = FindButton("QUIT");

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ContinueGame);
            continueButton.interactable = HasSaveData();
        }
        if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        loadingScreen = CreateLoadingScreen();
    }

    private void Start()
    {
        if (!preloadGameplayScene || !Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            return;

        preloadOperation = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Additive);
        if (preloadOperation != null)
            preloadOperation.allowSceneActivation = false;
    }

    public void ContinueGame()
    {
        if (HasSaveData())
            LoadGameplay();
    }

    public void NewGame()
    {
        if (!HasSaveData())
        {
            StartFreshGame();
            return;
        }

        ShowOverwritePrompt();
    }

    public void QuitGame() => Application.Quit();

    private bool HasSaveData() => PauseMenu.HasRigidbodySave() || PotiondexProgress.HasSavedProgress;

    private void StartFreshGame()
    {
        PauseMenu.ClearRigidbodySave();
        PotiondexProgress.ClearAll();
        LoadGameplay();
    }

    private void LoadGameplay()
    {
        if (loadingGameplay)
            return;

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError($"Main menu cannot load gameplay scene '{gameplaySceneName}'. Add it to Build Settings.", this);
            return;
        }

        loadingGameplay = true;
        if (continueButton != null)
            continueButton.interactable = false;

        if (preloadOperation != null)
            StartCoroutine(ActivatePreloadedGameplay());
        else
            StartCoroutine(LoadGameplayNormally());
    }

    private System.Collections.IEnumerator ActivatePreloadedGameplay()
    {
        yield return ShowLoadingScreen();

        // Unity holds an async scene load at 90% until activation is permitted.
        while (preloadOperation.progress < 0.9f)
            yield return null;

        preloadOperation.allowSceneActivation = true;
        while (!preloadOperation.isDone)
            yield return null;

        Scene gameplayScene = SceneManager.GetSceneByName(gameplaySceneName);
        if (gameplayScene.IsValid())
            SceneManager.SetActiveScene(gameplayScene);

        SceneManager.UnloadSceneAsync(gameObject.scene);
    }

    private System.Collections.IEnumerator LoadGameplayNormally()
    {
        yield return ShowLoadingScreen();
        SceneManager.LoadScene(gameplaySceneName);
    }

    private System.Collections.IEnumerator ShowLoadingScreen()
    {
        if (loadingScreen == null)
            yield break;

        loadingScreen.transform.SetAsLastSibling();
        loadingScreen.blocksRaycasts = true;
        float elapsed = 0f;
        while (elapsed < loadingFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            loadingScreen.alpha = Mathf.Clamp01(elapsed / loadingFadeDuration);
            yield return null;
        }
        loadingScreen.alpha = 1f;

        // Guarantees the completed overlay has a frame to render before scene activation.
        yield return null;
    }

    private Button FindButton(string buttonText)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.Trim().Equals(buttonText, System.StringComparison.OrdinalIgnoreCase))
                return button;
        }
        return null;
    }

    private void ShowOverwritePrompt()
    {
        if (overwritePrompt == null)
            overwritePrompt = CreateOverwritePrompt();
        overwritePrompt.SetActive(true);
    }

    private GameObject CreateOverwritePrompt()
    {
        TMP_FontAsset font = FindAnyObjectByType<TMP_Text>()?.font;
        GameObject root = CreateObject("New Game Warning", transform);
        Stretch(root.GetComponent<RectTransform>());
        root.transform.SetAsLastSibling();

        Image backdrop = root.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.62f);

        GameObject panel = CreateObject("Panel", root.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 350f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.12f, 0.075f, 0.16f, 0.98f);

        CreateText("Title", panel.transform, "START A NEW GAME?", font, 36f, FontStyles.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(640f, 55f));
        CreateText("Message", panel.transform, "This will overwrite your current saved progress.\nThis cannot be undone.", font, 24f, FontStyles.Normal,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(630f, 100f));

        Button cancel = CreateButton("Cancel", panel.transform, "CANCEL", font, new Vector2(-165f, -113f));
        cancel.onClick.AddListener(() => overwritePrompt.SetActive(false));
        Button confirm = CreateButton("Confirm New Game", panel.transform, "OVERWRITE SAVE", font, new Vector2(165f, -113f));
        confirm.onClick.AddListener(StartFreshGame);
        return root;
    }

    private CanvasGroup CreateLoadingScreen()
    {
        TMP_FontAsset font = FindAnyObjectByType<TMP_Text>()?.font;
        GameObject root = CreateObject("Loading Screen", transform);
        Stretch(root.GetComponent<RectTransform>());
        root.transform.SetAsLastSibling();
        Image backdrop = root.AddComponent<Image>();
        backdrop.color = new Color(0.025f, 0.018f, 0.05f, 1f);
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        CreateText("Loading Label", root.transform, "ENTERING THE SHOP...", font, 32f, FontStyles.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(700f, 60f));
        CreateText("Loading Detail", root.transform, "Preparing your potions", font, 20f, FontStyles.Italic,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(700f, 40f));
        return group;
    }

    private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font, Vector2 position)
    {
        GameObject buttonObject = CreateObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(260f, 58f);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.34f, 0.22f, 0.1f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.87f, 0.57f, 1f);
        colors.pressedColor = new Color(0.72f, 0.52f, 0.28f, 1f);
        button.colors = colors;
        CreateText("Label", buttonObject.transform, label, font, 20f, FontStyles.Bold,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, TMP_FontAsset font, float size, FontStyles style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
    {
        GameObject textObject = CreateObject(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = new Color(1f, 0.88f, 0.62f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateObject(string name, Transform parent)
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
}
