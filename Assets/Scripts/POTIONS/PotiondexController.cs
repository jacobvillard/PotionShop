using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Uses the authored Canvas/RootCodex artwork as a single pannable potion tree.
/// Square nodes use the supplied locked and category-icon examples; names and recipes live in hover tooltips.
/// </summary>
public sealed class PotiondexController : MonoBehaviour
{
    public sealed class Node { public string id, recipe, section; public Vector2 position; public float column; public int sectionIndex, row; public string[] prerequisites; }

    private readonly Dictionary<string, Node> nodes = new Dictionary<string, Node>();
    private readonly Dictionary<string, NodeView> views = new Dictionary<string, NodeView>();
    private readonly Dictionary<string, Sprite> categoryIcons = new Dictionary<string, Sprite>();
    private readonly List<Image> branches = new List<Image>();
    private readonly Dictionary<Image, string> branchTargets = new Dictionary<Image, string>();
    private readonly Dictionary<Image, BranchGeometry> branchGeometry = new Dictionary<Image, BranchGeometry>();
    private readonly Dictionary<Image, int> branchRoutes = new Dictionary<Image, int>();
    private readonly List<Image> revealOverlays = new List<Image>();
    private readonly List<GoldBorderPart> goldBorderParts = new List<GoldBorderPart>();
    private string drawingTargetId;
    private int drawingRouteId = -1;
    private int nextRouteId;
    private readonly Dictionary<string, TextMeshProUGUI> sectionLabels = new Dictionary<string, TextMeshProUGUI>();
    private GameObject root, tooltip;
    private RectTransform map, tooltipRect, magicalFlare;
    private Image magicalFlareImage;
    private TextMeshProUGUI tooltipText;
    private Sprite lockedIcon;
    private TMP_FontAsset font;
    private PotionRecipeBook recipes;
    private MainMenuCursor menuCursor;
    private PlayerController playerController;
    private bool playerControllerWasEnabled;
    private PotiondexSettings settings;
    private float zoom = 1f;
    private int layoutHash;
    private int openingViewHash;
    private int goldBorderHash;
    private bool previousDefaultOpen;
    private Sprite roundedSprite;
    private Sprite arrowSprite;
    private Coroutine revealRoutine;
    private bool isRevealAnimating;

    private readonly struct BranchGeometry
    {
        public readonly Vector2 start;
        public readonly Vector2 end;
        public BranchGeometry(Vector2 start, Vector2 end) { this.start = start; this.end = end; }
    }

    private sealed class GoldBorderPart
    {
        public RectTransform rect;
        public int side;
        public bool isCorner;
        public Vector2 cornerSign;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForScene()
    {
        if (FindFirstObjectByType<PotiondexController>() == null)
            new GameObject("Potiondex Controller").AddComponent<PotiondexController>();
    }

    private void Awake()
    {
        root = FindRootCodex();
        if (root == null) { enabled = false; return; }
        settings = root.GetComponent<PotiondexSettings>();
        if (settings == null) settings = root.AddComponent<PotiondexSettings>();

        recipes = Resources.Load<PotionRecipeBook>("PotionRecipeBook");
        RectTransform area = root.transform.Find("DraggableArea") as RectTransform;
        if (area == null) { Debug.LogError("RootCodex needs a DraggableArea child.", root); enabled = false; return; }

        CacheAuthoredIcons(area);
        BuildData();
        BuildMap(area);
        BuildMagicalBackdrop();
        BuildMagicalBorder();
        BuildTooltip();
        BuildCloseButton();
        Canvas rootCanvas = root.GetComponentInParent<Canvas>();
        menuCursor = rootCanvas == null ? null : rootCanvas.GetComponent<MainMenuCursor>();
        if (menuCursor == null && rootCanvas != null)
            menuCursor = rootCanvas.gameObject.AddComponent<MainMenuCursor>();
        root.SetActive(false);
        PotiondexProgress.Changed += OnProgressChanged;
    }

    private void Start()
    {
        previousDefaultOpen = settings.defaultOpen;
        if (settings.defaultOpen) SetOpen(true);
    }

    private void OnDestroy()
    {
        PotiondexProgress.Changed -= OnProgressChanged;
        PauseMenu.Instance?.SetExternalMenuCursor(false);
        if (playerController != null) playerController.enabled = playerControllerWasEnabled;
    }

    private static GameObject FindRootCodex()
    {
        // GameObject.Find ignores inactive hierarchy entries; RootCodex is intentionally
        // saved closed, so include inactive scene objects as well.
        RectTransform rootTransform = Resources.FindObjectsOfTypeAll<RectTransform>()
            .FirstOrDefault(candidate => candidate.name == "RootCodex" && candidate.gameObject.scene.IsValid());
        return rootTransform == null ? null : rootTransform.gameObject;
    }

    private void Update()
    {
        ApplyLayout();
        ApplyGoldBorderLayout();
        int currentOpeningViewHash = HashCode.Combine(settings.openingMapPosition, settings.openingZoom);
        if (currentOpeningViewHash != openingViewHash)
        {
            openingViewHash = currentOpeningViewHash;
            if (root.activeSelf) ResetToOpeningView();
        }
        if (settings.defaultOpen != previousDefaultOpen)
        {
            previousDefaultOpen = settings.defaultOpen;
            SetOpen(settings.defaultOpen);
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.kKey.wasPressedThisFrame) SetOpen(!root.activeSelf);
        if (root.activeSelf && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        // Do not refresh node visuals while a reveal is in progress: Refresh would
        // replace the lock before the travelling path has reached its destination.
        if (root.activeSelf && !isRevealAnimating) Refresh();
        if (magicalFlare != null)
        {
            magicalFlareImage.sprite = settings.magicalFlareSprite;
            magicalFlareImage.color = settings.magicalFlareColor;
            magicalFlare.sizeDelta = Vector2.one * settings.magicalFlareSize;
            magicalFlare.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 2.5f);
            float breath = 1f + Mathf.Sin(Time.unscaledTime * .65f) * .035f;
            magicalFlare.localScale = Vector3.one * breath;
        }
    }

    private void SetOpen(bool open)
    {
        if (root.activeSelf == open) return;
        if (open)
        {
            playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerControllerWasEnabled = playerController.enabled;
                playerController.enabled = false;
            }
            root.SetActive(true);
            ResetToOpeningView();
            RefreshKnownReveals();
            PauseMenu.Instance?.SetExternalMenuCursor(true);
            menuCursor?.SetCursorActive(true);
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = StartCoroutine(RevealNewUnlocks());
        }
        else
        {
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = null;
            isRevealAnimating = false;
            ClearRevealOverlays();
            root.SetActive(false);
            PauseMenu.Instance?.SetExternalMenuCursor(false);
            if (playerController != null) playerController.enabled = playerControllerWasEnabled;
            playerController = null;
            if (menuCursor != null) menuCursor.SetCursorActive(false);
            else { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
    }

    private void CacheAuthoredIcons(RectTransform area)
    {
        foreach (Image image in area.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null) continue;
            if (image.name == "Locked") lockedIcon = image.sprite;
            else if (image.name == "RESTORAITON ") categoryIcons["Restoration"] = image.sprite;
            else if (image.name == "Movement") categoryIcons["Movement"] = image.sprite;
            else if (image.name == "Transformation") categoryIcons["Transformation"] = image.sprite;
            else if (image.name == "Perception") categoryIcons["Perception"] = image.sprite;
            else if (image.name == "Elemental & Arcane") categoryIcons["Elemental & Arcane"] = image.sprite;
            else if (image.name == "Mischief & Utility") categoryIcons["Mischief & Utility"] = image.sprite;
        }
        font = root.GetComponentInChildren<TMP_Text>(true)?.font;
        foreach (Transform child in area) child.gameObject.SetActive(false); // Authored examples remain intact as hidden templates.
    }

    private void BuildMap(RectTransform area)
    {
        area.gameObject.AddComponent<RectMask2D>();
        area.gameObject.AddComponent<PotiondexPanSurface>().Initialize(this);
        map = NewRect("Potion Tree", area); map.anchorMin = map.anchorMax = new Vector2(.5f, .5f); map.pivot = new Vector2(.5f, 1f); map.sizeDelta = new Vector2(1900, EffectiveSectionGap * 6f + 420f); map.anchoredPosition = new Vector2(0, 400);
        foreach (Node node in nodes.Values) CreateNode(node);
        foreach (Node node in nodes.Values) foreach (string parent in node.prerequisites) DrawElbow(nodes[parent], node);
        foreach (Node node in nodes.Values) CreateSectionLabel(node);
        Refresh();
    }

    private void BuildMagicalBackdrop()
    {
        if (settings.magicalFlareSprite == null) return;
        // The DraggableArea's Image is opaque. Place the flare inside it so it
        // renders above that panel, but below the potion map.
        magicalFlareImage = NewImage("Magical Flare", map.parent);
        magicalFlareImage.sprite = settings.magicalFlareSprite;
        magicalFlareImage.color = settings.magicalFlareColor;
        magicalFlareImage.raycastTarget = false;
        magicalFlare = magicalFlareImage.rectTransform;
        magicalFlare.anchorMin = magicalFlare.anchorMax = new Vector2(.5f, .5f);
        magicalFlare.sizeDelta = Vector2.one * settings.magicalFlareSize;
        magicalFlare.SetSiblingIndex(0);
    }

    private void BuildMagicalBorder()
    {
        Color outer = new Color(.42f, .22f, .66f, .9f);
        Color inner = new Color(.78f, .61f, 1f, .78f);
        CreateBorderEdge("Top rune border", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -15f), new Vector2(-28f, -10f), outer);
        CreateBorderEdge("Bottom rune border", Vector2.zero, new Vector2(1f, 0f), new Vector2(28f, 10f), new Vector2(-28f, 15f), outer);
        CreateBorderEdge("Left rune border", Vector2.zero, new Vector2(0f, 1f), new Vector2(10f, 28f), new Vector2(15f, -28f), outer);
        CreateBorderEdge("Right rune border", new Vector2(1f, 0f), Vector2.one, new Vector2(-15f, 28f), new Vector2(-10f, -28f), outer);
        CreateBorderCrest(new Vector2(.5f, 1f), new Vector2(0f, -14f), 0f, inner);
        CreateBorderCrest(new Vector2(.5f, 0f), new Vector2(0f, 14f), 180f, inner);
        CreateBorderGem(new Vector2(0f, 1f), new Vector2(17f, -17f), inner);
        CreateBorderGem(new Vector2(1f, 1f), new Vector2(-17f, -17f), inner);
        CreateBorderGem(Vector2.zero, new Vector2(17f, 17f), inner);
        CreateBorderGem(new Vector2(1f, 0f), new Vector2(-17f, 17f), inner);
    }

    private void CreateBorderEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        Image edge = NewImage(name, root.transform); edge.sprite = settings.borderLineSprite != null ? settings.borderLineSprite : RoundedSprite; edge.type = Image.Type.Sliced; edge.color = color; edge.raycastTarget = false;
        edge.rectTransform.anchorMin = anchorMin; edge.rectTransform.anchorMax = anchorMax; edge.rectTransform.offsetMin = offsetMin; edge.rectTransform.offsetMax = offsetMax;
    }

    private void CreateBorderGem(Vector2 anchor, Vector2 position, Color color)
    {
        Image gem = NewImage("Rune gem", root.transform); gem.sprite = settings.borderGemSprite != null ? settings.borderGemSprite : RoundedSprite; gem.color = color; gem.raycastTarget = false;
        gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = anchor; gem.rectTransform.anchoredPosition = position; gem.rectTransform.sizeDelta = new Vector2(12f, 12f); gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private void CreateBorderCrest(Vector2 anchor, Vector2 position, float rotation, Color color)
    {
        Image crest = NewImage("Arcane crest", root.transform); crest.sprite = settings.borderCrestSprite != null ? settings.borderCrestSprite : RoundedSprite; crest.color = color; crest.raycastTarget = false;
        crest.rectTransform.anchorMin = crest.rectTransform.anchorMax = anchor; crest.rectTransform.anchoredPosition = position; crest.rectTransform.sizeDelta = new Vector2(92f, 22f); crest.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    private void CreateNode(Node node)
    {
        GameObject square = new GameObject(node.id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        square.transform.SetParent(map, false);
        RectTransform rect = square.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.sizeDelta = new Vector2(112f, 112f); rect.anchoredPosition = node.position;
        Image backing = square.GetComponent<Image>(); backing.sprite = RoundedSprite; backing.type = Image.Type.Sliced; backing.color = new Color(.12f, .09f, .17f, 1f);
        Image icon = NewImage("Icon", square.transform); icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f); icon.rectTransform.sizeDelta = new Vector2(84f, 84f);
        GameObject goldBorder = CreateGoldBorder(square.transform);
        NodeView view = square.AddComponent<NodeView>(); view.Initialize(this, node, backing, icon, goldBorder); views.Add(node.id, view);
    }

    private GameObject CreateGoldBorder(Transform parent)
    {
        GameObject border = NewRect("Unlocked Gold Border", parent).gameObject; border.SetActive(false);
        foreach (int side in new[] { 0, 1, 2, 3 })
        {
            Image line = NewImage("Gold border", border.transform); line.color = new Color(1f, .72f, .2f, 1f); line.raycastTarget = false;
            RectTransform rect = line.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            goldBorderParts.Add(new GoldBorderPart { rect = rect, side = side });
        }
        foreach (Vector2 sign in new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) })
        {
            Image cap = NewImage("Rounded gold corner", border.transform); cap.sprite = RoundedSprite; cap.color = new Color(1f, .72f, .2f, 1f); cap.raycastTarget = false; cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(.5f, .5f);
            goldBorderParts.Add(new GoldBorderPart { rect = cap.rectTransform, isCorner = true, cornerSign = sign });
        }
        ApplyGoldBorderLayout(true);
        return border;
    }

    private void ApplyGoldBorderLayout(bool force = false)
    {
        if (settings == null) return;
        int currentHash = HashCode.Combine(settings.unlockedBorderInset, settings.unlockedBorderThickness, settings.unlockedBorderCornerSize);
        if (!force && currentHash == goldBorderHash) return;
        goldBorderHash = currentHash;
        float halfSize = 56f + settings.unlockedBorderInset;
        float thickness = settings.unlockedBorderThickness;
        float cornerSize = settings.unlockedBorderCornerSize;
        float lineLength = halfSize * 2f + 4f;
        foreach (GoldBorderPart part in goldBorderParts)
        {
            if (part == null || part.rect == null) continue;
            if (part.isCorner)
            {
                part.rect.sizeDelta = Vector2.one * cornerSize;
                part.rect.anchoredPosition = Vector2.Scale(part.cornerSign, new Vector2(halfSize, halfSize));
            }
            else if (part.side < 2)
            {
                part.rect.sizeDelta = new Vector2(lineLength, thickness);
                part.rect.anchoredPosition = new Vector2(0f, part.side == 0 ? halfSize : -halfSize);
            }
            else
            {
                part.rect.sizeDelta = new Vector2(thickness, lineLength);
                part.rect.anchoredPosition = new Vector2(part.side == 2 ? halfSize : -halfSize, 0f);
            }
        }
    }

    private void DrawElbow(Node from, Node to)
    {
        drawingTargetId = to.id;
        drawingRouteId = nextRouteId++;
        const float halfNode = 56f;
        if (Mathf.Abs(from.position.y - to.position.y) < 1f)
        {
            Vector2 horizontalEnd = to.position - new Vector2(halfNode, 0);
            CreateLine(from.position + new Vector2(halfNode, 0), horizontalEnd);
            CreateArrow(horizontalEnd, 0f);
            drawingTargetId = null;
            drawingRouteId = -1;
            return;
        }
        if (Mathf.Abs(from.position.x - to.position.x) < .01f)
        {
            Vector2 start = from.position + new Vector2(0, -halfNode);
            Vector2 end = to.position + new Vector2(0, halfNode);
            CreateLine(start, end); // A pure vertical branch: never create a zero-length middle segment.
            CreateArrow(end, end.y < start.y ? -90f : 90f);
            drawingTargetId = null;
            drawingRouteId = -1;
            return;
        }
        bool goesRight = to.position.x > from.position.x;
        Vector2 startSide = from.position + new Vector2(goesRight ? halfNode : -halfNode, 0f);
        Vector2 endSide = to.position + new Vector2(goesRight ? -halfNode : halfNode, 0f);
        if (from.id == "Curing Panacea" && to.id == "Poisonous Venom")
        {
            // Align this exception to the visible 84px locked-icon square rather
            // than the larger 112px transparent backing rect.
            const float visibleIconHalf = 42f;
            startSide = from.position + new Vector2(visibleIconHalf, 0f);
            // Curing's line travels right, then rises into Venom at the bottom-centre.
            endSide = to.position + new Vector2(0f, -visibleIconHalf);
            Vector2 venomBottomCorner = new Vector2(to.position.x, startSide.y);
            CreateLine(startSide, venomBottomCorner);
            CreateLine(venomBottomCorner, endSide);
            CreateArrow(endSide, -90f);
            drawingTargetId = null;
            drawingRouteId = -1;
            return;
        }
        else
        {
            float midX = Mathf.Lerp(startSide.x, endSide.x, .5f);
            CreateLine(startSide, new Vector2(midX, startSide.y));
            CreateLine(new Vector2(midX, startSide.y), new Vector2(midX, endSide.y));
            CreateLine(new Vector2(midX, endSide.y), endSide);
        }
        CreateArrow(endSide, goesRight ? 0f : 180f);
        drawingTargetId = null;
        drawingRouteId = -1;
    }

    private void CreateLine(Vector2 start, Vector2 end)
    {
        Image line = NewImage("Flow branch", map); line.color = new Color(.18f, .2f, .28f, 1f); line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(.5f, 1f);
        Vector2 delta = end - start; bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
        line.rectTransform.sizeDelta = horizontal ? new Vector2(Mathf.Abs(delta.x), 3f) : new Vector2(3f, Mathf.Abs(delta.y));
        line.rectTransform.anchoredPosition = (start + end) * .5f; line.rectTransform.SetAsFirstSibling(); branches.Add(line);
        branchGeometry[line] = new BranchGeometry(start, end);
        if (drawingTargetId != null) branchTargets[line] = drawingTargetId;
        if (drawingRouteId >= 0) branchRoutes[line] = drawingRouteId;
    }

    private void CreateSectionLabel(Node node)
    {
        if (nodes.Values.Any(n => n != node && n.section == node.section && n.position.y > node.position.y)) return;
        TextMeshProUGUI label = NewText(node.section.ToUpperInvariant(), map, 24, new Color(.72f, .58f, .9f));
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 1f); label.rectTransform.sizeDelta = new Vector2(420, 36); label.rectTransform.anchoredPosition = new Vector2(-760, node.position.y + 132f) + settings.sectionTitleOffset; sectionLabels[node.section] = label;
    }

    private void BuildTooltip()
    {
        GameObject panel = new GameObject("Potion Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); panel.transform.SetParent(root.transform, false); tooltip = panel;
        Image image = panel.GetComponent<Image>(); image.sprite = RoundedSprite; image.type = Image.Type.Sliced; image.color = new Color(.55f, .4f, .72f, 1f); image.raycastTarget = false;
        tooltipRect = panel.GetComponent<RectTransform>(); tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0, 1); tooltipRect.pivot = new Vector2(0, 1); tooltipRect.sizeDelta = new Vector2(360, 260);
        Image fill = NewImage("Inset", panel.transform); fill.sprite = RoundedSprite; fill.type = Image.Type.Sliced; fill.color = new Color(.08f, .055f, .12f, .98f); fill.raycastTarget = false; Stretch(fill.rectTransform, 4f);
        tooltipText = NewText(string.Empty, fill.transform, 18, new Color(.94f, .9f, 1f)); tooltipText.raycastTarget = false; tooltipText.alignment = TextAlignmentOptions.TopLeft; tooltipText.rectTransform.anchorMin = tooltipText.rectTransform.anchorMax = new Vector2(0, 1); tooltipText.rectTransform.pivot = new Vector2(0, 1); tooltipText.rectTransform.anchoredPosition = new Vector2(14, -12);
        tooltip.SetActive(false);
    }

    private void BuildCloseButton()
    {
        Image buttonImage = NewImage("Close Potiondex", root.transform); buttonImage.sprite = RoundedSprite; buttonImage.type = Image.Type.Sliced; buttonImage.color = new Color(.35f, .16f, .2f, .95f);
        RectTransform rect = buttonImage.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f); rect.anchoredPosition = new Vector2(-38f, -38f); rect.sizeDelta = new Vector2(176f, 38f);
        Button button = buttonImage.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => SetOpen(false));
        TextMeshProUGUI text = NewText("CLOSE CODEX", buttonImage.transform, 14f, Color.white); text.alignment = TextAlignmentOptions.Center; Stretch(text.rectTransform, 0f);
    }

    internal bool IsAvailable(Node node) => settings.unlockAllForTesting || node.prerequisites.Length == 0 || node.prerequisites.All(id => PotiondexProgress.HasBrewed(nodes[id].recipe));
    private void Refresh()
    {
        foreach (NodeView view in views.Values) view.Refresh(IsAvailable(view.Node), categoryIcons.TryGetValue(view.Node.section, out Sprite icon) ? icon : lockedIcon, lockedIcon);
        RefreshBranches();
    }

    private void RefreshKnownReveals()
    {
        foreach (NodeView view in views.Values)
        {
            bool hasAlreadyBeenShown = view.Node.recipe == "Health" || PotiondexProgress.HasBeenRevealed(view.Node.recipe);
            bool showAsUnlocked = settings.unlockAllForTesting || (IsAvailable(view.Node) && hasAlreadyBeenShown);
            view.Refresh(showAsUnlocked, categoryIcons.TryGetValue(view.Node.section, out Sprite icon) ? icon : lockedIcon, lockedIcon);
        }
        RefreshBranches(true);
    }

    private void RefreshBranches(bool onlyRevealed = false)
    {
        foreach (Image branch in branches)
        {
            bool isUnlockedPath = branchTargets.TryGetValue(branch, out string target)
                && IsAvailable(nodes[target])
                && (!onlyRevealed || PotiondexProgress.HasBeenRevealed(nodes[target].recipe));
            branch.color = isUnlockedPath ? settings.unlockGold : new Color(.25f, .42f, .55f, .75f);
            SetBranchProgress(branch, 1f);
        }
    }

    private void OnProgressChanged()
    {
        if (root != null && root.activeSelf)
        {
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = StartCoroutine(RevealNewUnlocks());
        }
        // Keep the previous visual state while this panel is closed so its next
        // opening can play the reveal instead of jumping straight to the result.
    }

    private IEnumerator RevealNewUnlocks()
    {
        isRevealAnimating = true;
        ClearRevealOverlays();
        // Health is the starting formula, not a newly discovered unlock; it gets its gold frame immediately.
        PotiondexProgress.MarkRevealed("Health");
        List<NodeView> newViews = views.Values.Where(view => IsAvailable(view.Node) && ((!PotiondexProgress.HasBeenRevealed(view.Node.recipe) && view.Node.recipe != "Health") || PotiondexProgress.IsTestRevealRequested(view.Node.recipe))).ToList();
        if (newViews.Count == 0) { isRevealAnimating = false; yield break; }
        HashSet<string> targetIds = new HashSet<string>(newViews.Select(view => view.Node.id));
        // Keep the normal blue (or previously completed gold) route intact.
        // The travelling gold is a separate overlay drawn over the route.
        Dictionary<Image, Image> overlays = new Dictionary<Image, Image>();
        foreach (Image branch in branches)
            if (branchTargets.TryGetValue(branch, out string target) && targetIds.Contains(target))
                overlays.Add(branch, CreateRevealOverlay(branch));

        float elapsed = 0f;
        while (elapsed < settings.revealTravelDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float glow = Mathf.Clamp01(elapsed / settings.revealTravelDuration);
            HashSet<int> revealRoutes = new HashSet<int>(branches.Where(branch => branchTargets.TryGetValue(branch, out string target) && targetIds.Contains(target)).Select(branch => branchRoutes[branch]));
            foreach (int routeId in revealRoutes)
            {
                List<Image> route = branches.Where(branch => branchRoutes.TryGetValue(branch, out int route) && route == routeId).ToList();
                int lineCount = route.Count(branch => branchGeometry.ContainsKey(branch));
                int lineIndex = 0;
                foreach (Image branch in route)
                {
                    Image overlay = overlays[branch];
                    if (branchGeometry.ContainsKey(branch))
                    {
                        // Each elbow segment waits for the preceding one to finish.
                        SetBranchProgress(overlay, Mathf.Clamp01(glow * lineCount - lineIndex));
                        lineIndex++;
                    }
                    else
                    {
                        // The arrowhead arrives only when the final segment reaches it.
                        SetBranchProgress(overlay, glow);
                    }
                }
            }
            yield return null;
        }
        // At the endpoint the route is complete, but retain the lock while it
        // pulses.  The final Refresh below replaces it with the category icon.
        ClearRevealOverlays();
        RefreshBranches();
        foreach (NodeView view in newViews)
        {
            view.PlayUnlock(settings.unlockGold, settings.revealPulseDuration);
            PotiondexProgress.MarkRevealed(view.Node.recipe);
            PotiondexProgress.CompleteTestReveal(view.Node.recipe);
        }
        if (settings.unlockSfx != null) AudioSource.PlayClipAtPoint(settings.unlockSfx, Vector3.zero, .65f);
        yield return new WaitForSecondsRealtime(settings.revealPulseDuration);
        Refresh();
        isRevealAnimating = false;
        revealRoutine = null;
    }

    private void ApplyLayout()
    {
        if (map == null || settings == null) return;
        int currentHash = HashCode.Combine(settings.sectionTitleOffset, settings.horizontalBoxGap, settings.verticalBoxGap, settings.sectionGap, settings.minimumZoom, settings.maximumZoom, settings.zoomStep);
        if (currentHash == layoutHash) return;
        layoutHash = currentHash;
        map.sizeDelta = new Vector2(1900f, EffectiveSectionGap * 6f + 420f);
        foreach (Node node in nodes.Values)
        {
            node.position = PositionFor(node.column, node.sectionIndex, node.row);
            if (views.TryGetValue(node.id, out NodeView view)) view.GetComponent<RectTransform>().anchoredPosition = node.position;
        }
        foreach (Image branch in branches) Destroy(branch.gameObject);
        branches.Clear();
        branchTargets.Clear();
        branchGeometry.Clear();
        branchRoutes.Clear();
        ClearRevealOverlays();
        foreach (Node node in nodes.Values) foreach (string parent in node.prerequisites) DrawElbow(nodes[parent], node);
        foreach (KeyValuePair<string, TextMeshProUGUI> pair in sectionLabels)
        {
            Node first = nodes.Values.Where(node => node.section == pair.Key).OrderByDescending(node => node.position.y).First();
            pair.Value.rectTransform.anchoredPosition = new Vector2(-760f, first.position.y + 132f) + settings.sectionTitleOffset;
        }
        zoom = Mathf.Clamp(zoom, settings.minimumZoom, settings.maximumZoom);
        map.localScale = Vector3.one * zoom;
    }

    internal void ShowTooltip(Node node, Vector2 screenPosition)
    {
        if (!IsAvailable(node)) tooltipText.text = "<b>LOCKED FORMULA</b>\n\nBrew every potion leading into this square to reveal its formula.";
        else
        {
            PotionRecipe recipe = recipes == null ? null : recipes.FindByName(node.recipe);
            tooltipText.text = $"<b>{node.id}</b>\n<size=75%>{node.section}</size>\n\n<b>INGREDIENTS</b>\n{PotionRecipeBook.DescribeIngredients(recipe)}";
        }
        Vector2 preferred = tooltipText.GetPreferredValues(400f, 0f);
        float width = Mathf.Clamp(preferred.x + 28f, 210f, 430f);
        float height = Mathf.Clamp(tooltipText.GetPreferredValues(width - 28f, 0f).y + 26f, 72f, 360f);
        tooltipRect.localScale = Vector3.one * settings.tooltipScale;
        tooltipRect.sizeDelta = new Vector2(width, height);
        tooltipText.rectTransform.sizeDelta = new Vector2(width - 28f, height - 24f);
        float scaledWidth = width * settings.tooltipScale;
        float scaledHeight = height * settings.tooltipScale;
        float x = screenPosition.x + 18f;
        float y = screenPosition.y - 18f;
        if (x + scaledWidth > Screen.width) x = screenPosition.x - scaledWidth - 18f;
        if (y - scaledHeight < 0f) y = screenPosition.y + scaledHeight + 18f;
        tooltipRect.position = new Vector2(x, y); tooltip.SetActive(true);
    }
    internal void HideTooltip() => tooltip.SetActive(false);
    internal void Pan(Vector2 delta) { map.anchoredPosition += delta * settings.dragSpeed; ClampMapPosition(); }
    internal void Zoom(float wheelDelta)
    {
        if (Mathf.Approximately(wheelDelta, 0f)) return;
        zoom = Mathf.Clamp(zoom + wheelDelta * settings.zoomStep, settings.minimumZoom, settings.maximumZoom);
        map.localScale = Vector3.one * zoom;
        ClampMapPosition();
    }

    private void ResetToOpeningView()
    {
        zoom = Mathf.Clamp(settings.openingZoom, settings.minimumZoom, settings.maximumZoom);
        map.localScale = Vector3.one * zoom;
        map.anchoredPosition = settings.openingMapPosition;
        ClampMapPosition();
    }

    private void ClampMapPosition()
    {
        if (map == null || map.parent is not RectTransform viewport) return;
        float halfViewportWidth = viewport.rect.width * .5f;
        float halfViewportHeight = viewport.rect.height * .5f;
        float halfMapWidth = map.rect.width * map.localScale.x * .5f;
        float mapHeight = map.rect.height * map.localScale.y;
        float minX = Mathf.Min(halfViewportWidth - halfMapWidth, halfMapWidth - halfViewportWidth);
        float maxX = Mathf.Max(halfViewportWidth - halfMapWidth, halfMapWidth - halfViewportWidth);
        float minY = halfViewportHeight;
        float maxY = Mathf.Max(halfViewportHeight, mapHeight - halfViewportHeight);
        map.anchoredPosition = new Vector2(Mathf.Clamp(map.anchoredPosition.x, minX, maxX), Mathf.Clamp(map.anchoredPosition.y, minY, maxY));
    }

    private static RectTransform NewRect(string name, Transform parent) { GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>(); }
    private static Image NewImage(string name, Transform parent) { GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); return go.GetComponent<Image>(); }
    private TextMeshProUGUI NewText(string value, Transform parent, float size, Color color) { GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.textWrappingMode = TextWrappingModes.Normal; return text; }
    private Sprite RoundedSprite => roundedSprite ??= CreateRoundedSprite();
    private Sprite ArrowSprite => arrowSprite ??= CreateArrowSprite();
    private void CreateArrow(Vector2 position, float angle)
    {
        Image arrow = NewImage("Arrow", map); arrow.sprite = ArrowSprite; arrow.color = new Color(.35f, .72f, .82f, 1f); arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(.5f, 1f); arrow.rectTransform.sizeDelta = new Vector2(14f, 14f); arrow.rectTransform.anchoredPosition = position; arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle); arrow.rectTransform.SetAsFirstSibling(); branches.Add(arrow);
        if (drawingTargetId != null) branchTargets[arrow] = drawingTargetId;
        if (drawingRouteId >= 0) branchRoutes[arrow] = drawingRouteId;
    }

    private void SetBranchProgress(Image branch, float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (!branchGeometry.TryGetValue(branch, out BranchGeometry geometry))
        {
            // Arrowheads appear only as the line reaches its target.
            branch.gameObject.SetActive(progress >= .98f);
            return;
        }

        branch.gameObject.SetActive(true);
        Vector2 delta = geometry.end - geometry.start;
        bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
        RectTransform rect = branch.rectTransform;
        rect.sizeDelta = horizontal
            ? new Vector2(Mathf.Abs(delta.x) * progress, 3f)
            : new Vector2(3f, Mathf.Abs(delta.y) * progress);
        // Position the shortened segment from its source end, not from its centre.
        rect.anchoredPosition = geometry.start + delta * (progress * .5f);
    }

    private Image CreateRevealOverlay(Image source)
    {
        Image overlay = NewImage("Travelling gold", map);
        overlay.sprite = source.sprite;
        overlay.type = source.type;
        overlay.color = settings.unlockGold;
        overlay.raycastTarget = false;
        RectTransform destination = overlay.rectTransform;
        RectTransform origin = source.rectTransform;
        destination.anchorMin = origin.anchorMin;
        destination.anchorMax = origin.anchorMax;
        destination.pivot = origin.pivot;
        destination.sizeDelta = origin.sizeDelta;
        destination.anchoredPosition = origin.anchoredPosition;
        destination.localRotation = origin.localRotation;
        // Place it immediately above its blue source, still below the node icons.
        destination.SetSiblingIndex(Mathf.Min(origin.GetSiblingIndex() + 1, map.childCount - 1));
        if (branchGeometry.TryGetValue(source, out BranchGeometry geometry)) branchGeometry[overlay] = geometry;
        revealOverlays.Add(overlay);
        SetBranchProgress(overlay, 0f);
        return overlay;
    }

    private void ClearRevealOverlays()
    {
        foreach (Image overlay in revealOverlays)
        {
            branchGeometry.Remove(overlay);
            if (overlay != null) Destroy(overlay.gameObject);
        }
        revealOverlays.Clear();
    }
    private static Sprite CreateRoundedSprite()
    {
        const int size = 32, radius = 7; Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { float dx = Mathf.Max(radius - x, x - (size - radius - 1), 0); float dy = Mathf.Max(radius - y, y - (size - radius - 1), 0); texture.SetPixel(x, y, new Color(1, 1, 1, dx * dx + dy * dy <= radius * radius ? 1 : 0)); }
        texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
    private static Sprite CreateArrowSprite()
    {
        const int size = 32; Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { bool inside = x >= 4 && x <= 28 && Mathf.Abs(y - 16) <= (x - 4) * .5f; texture.SetPixel(x, y, new Color(1, 1, 1, inside ? 1 : 0)); }
        texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
    }
    private static void Stretch(RectTransform rect, float inset) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset); }

    private void BuildData()
    {
        Add("Potion of Greater Healing", "Health", "Restoration", 140, -170); Add("Potion of Regeneration", "Regeneration", "Restoration", 510, -170, "Potion of Greater Healing"); Add("Potion of Rejuvenation", "Rejuvenation", "Restoration", 880, -170, "Potion of Regeneration"); Add("Curing Panacea", "Antidote", "Restoration", 510, -380, "Potion of Greater Healing"); Add("Strength Aether", "Strength", "Restoration", 880, -300, "Potion of Regeneration"); Add("Poisonous Venom", "Poison", "Restoration", 1250, -300, "Curing Panacea", "Strength Aether"); Add("Potion of Resurrection", "Resurrection", "Restoration", 1620, -170, "Potion of Rejuvenation", "Poisonous Venom");
        Add("Speed", "Speed", "Movement", 140, -850, "Potion of Greater Healing"); Add("Levitation", "Levitation", "Movement", 510, -850, "Speed"); Add("Jump", "Jump", "Movement", 510, -1050, "Speed"); Add("Featherfall", "Feather Fall", "Movement", 880, -850, "Levitation"); Add("Dash", "Dash", "Movement", 880, -1050, "Jump"); Add("Gravity Shift Concoction", "Teleport", "Movement", 1250, -850, "Featherfall"); Add("Ethereal Passage", "Phase Walk", "Movement", 1620, -950, "Gravity Shift Concoction", "Dash");
        Add("Elixir of Trangenderism", "Gender Change", "Transformation", 140, -1530, "Speed"); Add("Elixir of Therianthropy", "Elixir of Therianthropy", "Transformation", 510, -1530, "Elixir of Trangenderism"); Add("Diminution", "Miniaturisation", "Transformation", 510, -1730, "Elixir of Trangenderism"); Add("Elixir of Metamorphosis", "Shapeshift", "Transformation", 880, -1530, "Elixir of Therianthropy"); Add("Colossal Growth", "Gigantism", "Transformation", 880, -1730, "Diminution"); Add("Invisibility", "Invisibility", "Transformation", 1250, -1530, "Elixir of Metamorphosis"); Add("False Visage", "Disguise", "Transformation", 1620, -1530, "Invisibility");
        Add("Nocturnal Sight", "Night Vision", "Perception", 140, -2210, "Elixir of Trangenderism"); Add("Clairvoyance", "Clarity", "Perception", 510, -2210, "Nocturnal Sight"); Add("Truesight", "Truth Sight", "Perception", 880, -2210, "Clairvoyance"); Add("Oneiromancy", "Dream Sight", "Perception", 880, -2410, "Clairvoyance"); Add("Foresight", "Foresight", "Perception", 1250, -2210, "Truesight"); Add("Spectral Vision", "Spirit Sight", "Perception", 1250, -2410, "Oneiromancy"); Add("Echolocation Infusion", "Echo Sense", "Perception", 1620, -2210, "Foresight", "Spectral Vision");
        Add("Mana Restoration", "Mana Restoration", "Elemental & Arcane", 140, -2890, "Nocturnal Sight"); Add("Summoning Draught", "Summoning", "Elemental & Arcane", 510, -2890, "Mana Restoration"); Add("Cryomancy Potion", "Frost", "Elemental & Arcane", 510, -3090, "Mana Restoration"); Add("Arcane Aegis", "Magic Ward", "Elemental & Arcane", 880, -2890, "Summoning Draught"); Add("Pyromancy Potion", "Fire Breath", "Elemental & Arcane", 880, -3090, "Cryomancy Potion"); Add("Electromancy Potion", "Lightning", "Elemental & Arcane", 1250, -3090, "Pyromancy Potion"); Add("Arcane Mimicry", "Duplication", "Elemental & Arcane", 1620, -2890, "Arcane Aegis", "Electromancy Potion");
        Add("Fortune's Favour", "Luck", "Mischief & Utility", 140, -3570, "Mana Restoration"); Add("Armorous Persuasion", "Love", "Mischief & Utility", 510, -3570, "Fortune's Favour"); Add("Bottle o' Confusion", "Confusion", "Mischief & Utility", 510, -3770, "Fortune's Favour"); Add("Bottle o' Serenity", "Content", "Mischief & Utility", 880, -3570, "Armorous Persuasion"); Add("Luminescence", "Glow", "Mischief & Utility", 880, -3690, "Armorous Persuasion"); Add("Bottle o' Sleep", "Sleep", "Mischief & Utility", 880, -3770, "Bottle o' Confusion"); Add("Death's Embrace", "Death", "Mischief & Utility", 1250, -3770, "Bottle o' Sleep");
    }
    private void Add(string id, string recipe, string section, float x, float y, params string[] prerequisites)
    {
        float column = (x - 140f) / 370f;
        int sectionIndex = section == "Restoration" ? 0 : section == "Movement" ? 1 : section == "Transformation" ? 2 : section == "Perception" ? 3 : section == "Elemental & Arcane" ? 4 : 5;
        int row = section == "Mischief & Utility" ? (y <= -3750f ? 2 : y <= -3650f ? 1 : 0) :
            section == "Restoration" ? (y <= -350f ? 2 : y <= -280f ? 1 : 0) :
            section == "Movement" ? (y <= -1000f ? 1 : 0) :
            section == "Transformation" ? (y <= -1650f ? 1 : 0) :
            section == "Perception" ? (y <= -2300f ? 1 : 0) :
            (y <= -3000f ? 1 : 0);
        nodes.Add(id, new Node { id = id, recipe = recipe, section = section, column = column, sectionIndex = sectionIndex, row = row, position = PositionFor(column, sectionIndex, row), prerequisites = prerequisites ?? Array.Empty<string>() });
    }

    private float EffectiveSectionGap => Mathf.Max(settings.sectionGap, settings.verticalBoxGap * 3f);
    private Vector2 PositionFor(float column, int sectionIndex, int row) => new Vector2((column - 2f) * settings.horizontalBoxGap, -(sectionIndex * EffectiveSectionGap + row * settings.verticalBoxGap + 140f));
}

internal sealed class NodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public PotiondexController.Node Node { get; private set; }
    private PotiondexController owner; private Image backing, icon; private GameObject goldBorder;
    public void Initialize(PotiondexController controller, PotiondexController.Node node, Image background, Image image, GameObject border) { owner = controller; Node = node; backing = background; icon = image; goldBorder = border; }
    public void Refresh(bool available, Sprite categoryIcon, Sprite lockedIcon) { icon.sprite = available ? categoryIcon : lockedIcon; icon.color = available ? Color.white : new Color(.56f, .5f, .65f, 1); backing.color = available ? new Color(.19f, .13f, .27f, 1f) : new Color(.075f, .055f, .1f, 1); goldBorder.SetActive(available); }
    public void PlayUnlock(Color gold, float duration) { StartCoroutine(UnlockPulse(gold, duration)); }
    private IEnumerator UnlockPulse(Color gold, float duration)
    {
        Vector3 initial = transform.localScale;
        Color initialColor = backing.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float wave = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            transform.localScale = initial * (1f + wave * .2f);
            backing.color = Color.Lerp(initialColor, gold, wave);
            yield return null;
        }
        transform.localScale = initial;
        backing.color = new Color(.19f, .13f, .27f, 1f);
        // PotiondexController applies the persistent unlocked frame only after
        // this pulse completes, so it is not visible at the start of the reveal.
        goldBorder.SetActive(false);
    }
    public void OnPointerEnter(PointerEventData eventData) => owner.ShowTooltip(Node, eventData.position);
    public void OnPointerExit(PointerEventData eventData) => owner.HideTooltip();
}

internal sealed class PotiondexPanSurface : MonoBehaviour, IDragHandler, IScrollHandler
{
    private PotiondexController owner;
    public void Initialize(PotiondexController controller) => owner = controller;
    public void OnDrag(PointerEventData eventData) => owner.Pan(eventData.delta);
    public void OnScroll(PointerEventData eventData) => owner.Zoom(eventData.scrollDelta.y);
}
