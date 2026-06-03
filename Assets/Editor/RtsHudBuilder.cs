#if UNITY_EDITOR
using System.Collections.Generic;
using Strategy.Core;
using Strategy.Buildings;
using TMPro;
using Strategy.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

using Strategy.Data;
using Strategy.Units;

/// <summary>
/// Редакторний інструмент, що повністю перебудовує RTS HUD всередині mainScene з нуля.
/// Викликається через Tools/RTS/Build RTS HUD. Створює ієрархію Canvas (верхня панель ресурсів,
/// нижній HUD з мінімапою, панеллю інформації про вибір та командною палубою), підключає всі UI-компоненти
/// та надає метод Validate для виклику з CI.
/// </summary>
public static class RtsHudBuilder
{
    private const string MainScenePath = "Assets/Scenes/mainScene.unity";
    private const string HeavyFactoryDataPath = "Assets/Balance/HeavyFactory.asset";
    private const string TopResourcesPrefabPath = "Assets/Prefabs/UI/TopResources.prefab";
    private const string ProductionButtonPrefabPath = "Assets/Prefabs/UI/ProductionButtonPrefab.prefab";
    private const string SelectionUnitCardPrefabPath = "Assets/Prefabs/UI/SelectionUnitCard.prefab";
    private const float SelectionSlotSize = 76f;
    private const float SelectionSlotSpacing = 8f;
    private const float ControlGroupSlotWidth = 58f;
    private const float ControlGroupSlotHeight = 48f;
    private const float ControlGroupSlotSpacing = 6f;
    private const int MaxSelectionUnitCards = 8;
    private const string SfWindowPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Window.psd";
    private const string SfGenericPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Generic.psd";
    private const string SfButtonPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Button.psd";
    private const string JupiterFontPath = "Assets/Unity UI Samples/Fonts/Jupiter/Jupiter.ttf";
    private const string JupiterTmpFontPath = "Assets/Unity UI Samples/Fonts/Jupiter/Jupiter TMP.asset";

    private static readonly Color PanelColor = Color.white;
    private static readonly Color PanelColorStrong = Color.white;
    private static readonly Color TextColor = new Color(0.04f, 0.075f, 0.11f, 1f);
    private static readonly Color MutedTextColor = new Color(0.22f, 0.34f, 0.44f, 1f);
    private static readonly Color ButtonColor = new Color(0f, 0.55f, 1f, 1f);
    private static readonly Color ButtonTextColor = Color.white;

    private static TMP_FontAsset _uiFontAsset;

    /// <summary>
    /// Головна точка входу побудови. Відкриває mainScene, очищає canvas, відтворює повну ієрархію HUD,
    /// підключає всі посилання компонентів та зберігає сцену.
    /// </summary>
    [MenuItem("Tools/RTS/Build RTS HUD")]
    public static void Build()
    {
        _uiFontAsset = LoadOrCreateFontAsset();

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Canvas canvas = EnsureCanvas();
        EnsureEventSystem();
        ClearCanvas(canvas);
        RemoveLegacyResourceStatus();
        UnitSelectionMetadataInstaller.Install();

        Sprite windowSprite = LoadSprite(SfWindowPath);
        Sprite genericSprite = LoadSprite(SfGenericPath);
        Sprite buttonSprite = LoadSprite(SfButtonPath);
        SelectionUnitCardUI selectionUnitCardPrefab = EnsureSelectionUnitCardPrefab(genericSprite);

        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform hudRoot = CreateRect("RtsHudRoot", canvas.transform, Vector2.zero, Vector2.one);
        hudRoot.offsetMin = Vector2.zero;
        hudRoot.offsetMax = Vector2.zero;

        InstantiateTopResourcesPrefab(hudRoot);

        RectTransform bottomHud = CreatePanel("BottomHud", hudRoot, windowSprite, PanelColorStrong);

        RectTransform minimap = CreatePanel("MinimapSlot", bottomHud, genericSprite, PanelColor);
        TMP_Text mapTitle = CreateText("MapTitle", minimap, "MAP", 20f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(mapTitle.rectTransform, new Vector2(10f, 54f), new Vector2(-10f, -42f));
        TMP_Text mapHint = CreateText("MapHint", minimap, "minimap reserved", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(mapHint.rectTransform, new Vector2(8f, 20f), new Vector2(-8f, -76f));

        RectTransform infoPanel = CreatePanel("SelectionInfoPanel", bottomHud, genericSprite, PanelColor);
        BuildSelectionInfo(infoPanel, selectionUnitCardPrefab);

        RectTransform controlGroupBar = CreateRect("ControlGroupBar", bottomHud, Vector2.zero, Vector2.one);
        BuildControlGroupBar(controlGroupBar, buttonSprite);

        RectTransform commandDeck = CreatePanel("CommandDeck", bottomHud, genericSprite, PanelColor);
        CommandPanels commandPanels = BuildCommandDeck(commandDeck, buttonSprite);

        UIManager manager = Object.FindFirstObjectByType<UIManager>();
        if (manager == null)
            manager = commandDeck.gameObject.AddComponent<UIManager>();
        ConfigureUIManager(manager, commandPanels);

        RtsHudResponsiveLayout layout = hudRoot.gameObject.AddComponent<RtsHudResponsiveLayout>();
        SetObject(layout, "_canvasRoot", canvasRect);
        SetObject(layout, "_bottomHud", bottomHud);
        SetObject(layout, "_minimapSlot", minimap);
        SetObject(layout, "_selectionInfoPanel", infoPanel);
        SetObject(layout, "_controlGroupBar", controlGroupBar);
        SetObject(layout, "_commandDeck", commandDeck);
        layout.ApplyLayout();

        EnsureUnitControlGroupController();

        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("RTS HUD rebuilt in mainScene.");
    }

    /// <summary>
    /// Точка входу для беззупинної валідації з CI. Перевіряє масштабування canvas, всі необхідні об'єкти HUD,
    /// наявність компонентів, список панелей UIManager та структуру префабу ProductionButton.
    /// </summary>
    public static void Validate()
    {
        List<string> errors = new List<string>();
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            errors.Add("Canvas is missing.");
        }
        else
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                errors.Add("CanvasScaler is missing.");
            else
            {
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    errors.Add("CanvasScaler should use Scale With Screen Size.");

                if (scaler.referenceResolution != new Vector2(1920f, 1080f))
                    errors.Add("CanvasScaler reference resolution should be 1920x1080.");
            }
        }

        ValidateObject("RtsHudRoot", errors);
        ValidateObject("TopResources", errors);
        ValidateObject("BottomHud", errors);
        ValidateObject("MinimapSlot", errors);
        ValidateObject("SelectionInfoPanel", errors);
        ValidateObject("ControlGroupBar", errors);
        ValidateObject("CommandDeck", errors);
        ValidateResourceHud(errors);
        ValidateBottomHudFrame(errors);
        ValidateProductionButtonPrefab(errors);
        ValidateSelectionUnitCardPrefab(errors);

        if (Object.FindFirstObjectByType<RtsHudResponsiveLayout>() == null)
            errors.Add("RtsHudResponsiveLayout is missing.");

        if (Object.FindFirstObjectByType<SelectionInfoPanelUI>() == null)
            errors.Add("SelectionInfoPanelUI is missing.");

        if (Object.FindFirstObjectByType<ControlGroupBarUI>() == null)
            errors.Add("ControlGroupBarUI is missing.");

        if (Object.FindFirstObjectByType<UnitControlGroupController>() == null)
            errors.Add("UnitControlGroupController is missing.");

        if (Object.FindFirstObjectByType<ConstructionPanelUI>(FindObjectsInactive.Include) == null)
            errors.Add("ConstructionPanelUI is missing.");

        if (Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include) == null)
            errors.Add("ProductionPanelUI is missing.");

        if (Object.FindFirstObjectByType<OutpostPanelUI>(FindObjectsInactive.Include) == null)
            errors.Add("OutpostPanelUI is missing.");

        UIManager manager = Object.FindFirstObjectByType<UIManager>();
        if (manager == null)
            errors.Add("UIManager is missing.");
        else
            ValidatePanelList(manager, errors);

        if (errors.Count > 0)
        {
            for (int i = 0; i < errors.Count; i++)
                Debug.LogError(errors[i]);

            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("RTS HUD validation passed.");
    }

    /// <summary>
    /// Знаходить або створює Canvas сцени та налаштовує його з CanvasScaler у режимі
    /// Scale With Screen Size на 1920x1080 та GraphicRaycaster.
    /// </summary>
    private static Canvas EnsureCanvas()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    /// <summary>
    /// Створює EventSystem з InputSystemUIInputModule, якщо такого об'єкта ще немає в сцені.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    /// <summary>
    /// Додає UnitControlGroupController на той самий об'єкт, що й UnitCommandController, щоб hotkeys
    /// працювали через пряме serialized-посилання на selection-контролер.
    /// </summary>
    private static void EnsureUnitControlGroupController()
    {
        UnitCommandController selectionController = Object.FindFirstObjectByType<UnitCommandController>();

        if (selectionController == null)
            return;

        UnitControlGroupController controlGroups =
            selectionController.GetComponent<UnitControlGroupController>() ??
            selectionController.gameObject.AddComponent<UnitControlGroupController>();

        SetObject(controlGroups, "_selectionController", selectionController);
        EditorUtility.SetDirty(controlGroups);
    }

    /// <summary>
    /// Знищує всіх прямих нащадків canvas, щоб HUD можна було відтворити з нуля.
    /// </summary>
    private static void ClearCanvas(Canvas canvas)
    {
        List<GameObject> children = new List<GameObject>();

        foreach (Transform child in canvas.transform)
            children.Add(child.gameObject);

        for (int i = 0; i < children.Count; i++)
            Object.DestroyImmediate(children[i]);
    }

    /// <summary>
    /// Видаляє всі компоненти OutpostStatusUI, що НЕ знаходяться на канонічному об'єкті "TopResources",
    /// прибираючи застарілі компоненти з попередніх макетів HUD.
    /// </summary>
    private static void RemoveLegacyResourceStatus()
    {
        OutpostStatusUI[] statuses = Object.FindObjectsByType<OutpostStatusUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < statuses.Length; i++)
        {
            OutpostStatusUI status = statuses[i];
            if (status == null || status.gameObject.name == "TopResources")
                continue;

            Object.DestroyImmediate(status, true);
        }
    }

    /// <summary>
    /// Додає до HUD готовий TopResources prefab. Візуальна структура, кольори,
    /// відступи та serialized references редагуються в Unity Inspector.
    /// </summary>
    private static RectTransform InstantiateTopResourcesPrefab(Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TopResourcesPrefabPath);
        if (prefab == null)
            throw new System.InvalidOperationException("TopResources prefab is missing at " + TopResourcesPrefabPath + ".");

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            throw new System.InvalidOperationException("TopResources prefab could not be instantiated.");

        instance.name = "TopResources";
        instance.transform.SetParent(parent, false);

        RectTransform rect = instance.GetComponent<RectTransform>();
        if (rect == null)
            throw new System.InvalidOperationException("TopResources prefab should have RectTransform.");

        return rect;
    }

    /// <summary>
    /// Створює або оновлює prefab картки вибраного типу юнітів. 8 таких карток по 76px зі spacing 8px
    /// займають 632px, що вміщується у правій смузі SelectionInfoPanel у цільовому широкому HUD.
    /// </summary>
    private static SelectionUnitCardUI EnsureSelectionUnitCardPrefab(Sprite genericSprite)
    {
        GameObject rootObject = new GameObject(
            "SelectionUnitCard",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement));

        RectTransform root = (RectTransform)rootObject.transform;
        root.sizeDelta = new Vector2(SelectionSlotSize, SelectionSlotSize);

        Image background = rootObject.GetComponent<Image>();
        background.color = new Color(0.018f, 0.027f, 0.036f, 0.94f);
        background.sprite = genericSprite;
        background.type = genericSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.raycastTarget = false;

        LayoutElement layout = rootObject.GetComponent<LayoutElement>();
        layout.minWidth = SelectionSlotSize;
        layout.preferredWidth = SelectionSlotSize;
        layout.minHeight = SelectionSlotSize;
        layout.preferredHeight = SelectionSlotSize;

        Outline outline = rootObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.68f, 0.92f, 1f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);
        AddBorder(root, new Color(0.92f, 1f, 1f, 0.72f), 2f);

        RectTransform frameBackground = CreatePanel("FrameBackground", root, null, new Color(0.07f, 0.12f, 0.15f, 0.96f));
        Stretch(frameBackground, new Vector2(5f, 5f), new Vector2(-5f, -5f));
        AddBorder(frameBackground, new Color(0.24f, 0.52f, 0.68f, 0.82f), 1.5f);

        RectTransform iconFrame = CreatePanel("IconFrame", frameBackground, null, new Color(0.03f, 0.05f, 0.07f, 0.92f));
        Stretch(iconFrame, new Vector2(5f, 5f), new Vector2(-5f, -5f));

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        iconObject.transform.SetParent(iconFrame, false);
        RectTransform iconRect = (RectTransform)iconObject.transform;
        Stretch(iconRect, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        Image icon = iconObject.GetComponent<Image>();
        icon.enabled = false;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text fallback = CreateText("Fallback", iconFrame, "UNIT", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(fallback.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        fallback.color = Color.white;

        TMP_Text name = CreateText("Name", root, "Unit", 10f, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchor(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 2f), new Vector2(-8f, 16f));
        name.color = Color.white;
        name.gameObject.SetActive(false);

        RectTransform countBadge = CreatePanel("CountBadge", root, null, new Color(0.01f, 0.42f, 0.72f, 0.98f));
        SetAnchor(countBadge, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-5f, 5f), new Vector2(28f, 24f));
        AddBorder(countBadge, new Color(0.82f, 0.96f, 1f, 0.86f), 1f);

        TMP_Text count = CreateText("Count", countBadge, "1", 15f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(count.rectTransform, Vector2.zero, Vector2.zero);
        count.color = Color.white;

        SelectionUnitCardUI card = rootObject.AddComponent<SelectionUnitCardUI>();
        SetObject(card, "_iconImage", icon);
        SetObject(card, "_fallbackText", fallback);
        SetObject(card, "_countText", count);
        SetObject(card, "_nameText", name);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(rootObject, SelectionUnitCardPrefabPath);
        Object.DestroyImmediate(rootObject);

        SelectionUnitCardUI savedCard = saved != null
            ? saved.GetComponent<SelectionUnitCardUI>()
            : AssetDatabase.LoadAssetAtPath<SelectionUnitCardUI>(SelectionUnitCardPrefabPath);

        if (savedCard == null)
            throw new System.InvalidOperationException("SelectionUnitCard prefab could not be created.");

        return savedCard;
    }

    /// <summary>
    /// Заповнює SelectionInfoPanel заголовком, текстом статистики та grid-контейнером selection-карток,
    /// після чого прикріплює SelectionInfoPanelUI та підключає всі серіалізовані посилання.
    /// </summary>
    private static void BuildSelectionInfo(RectTransform parent, SelectionUnitCardUI cardPrefab)
    {
        TMP_Text title = CreateText("SelectionTitle", parent, "No selection", 28f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -12f), new Vector2(-32f, 32f));

        TMP_Text subtitle = CreateText("SelectionSubtitle", parent, "Select units or buildings", 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        subtitle.color = MutedTextColor;
        SetAnchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -43f), new Vector2(-32f, 24f));

        TMP_Text stats = CreateText("SelectionStats", parent, "Orders and object data will appear here.", 18f, FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        SetAnchor(stats.rectTransform, new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(0f, 0f),
            new Vector2(16f, 14f), new Vector2(-20f, -74f));

        RectTransform cardRoot = CreateRect("SelectedUnitCards", parent, Vector2.zero, Vector2.one);
        cardRoot.offsetMin = new Vector2(14f, 14f);
        cardRoot.offsetMax = new Vector2(-14f, -14f);
        cardRoot.gameObject.SetActive(false);
        GridLayoutGroup cardGrid = cardRoot.gameObject.AddComponent<GridLayoutGroup>();
        cardGrid.cellSize = new Vector2(SelectionSlotSize, SelectionSlotSize);
        cardGrid.spacing = new Vector2(SelectionSlotSpacing, SelectionSlotSpacing);
        cardGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        cardGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        cardGrid.childAlignment = TextAnchor.UpperLeft;
        cardGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cardGrid.constraintCount = MaxSelectionUnitCards;

        RectTransform legacyListRoot = CreateRect("SelectionCompactList", parent, new Vector2(1f, 1f), Vector2.one);
        legacyListRoot.gameObject.SetActive(false);
        TMP_Text rowPrefab = CreateText("CompactRowPrefab", legacyListRoot, "Unit", 14f, FontStyles.Normal, TextAlignmentOptions.Left);
        rowPrefab.gameObject.SetActive(false);

        SelectionInfoPanelUI selectionInfo = parent.gameObject.AddComponent<SelectionInfoPanelUI>();
        SetObject(selectionInfo, "_titleText", title);
        SetObject(selectionInfo, "_subtitleText", subtitle);
        SetObject(selectionInfo, "_statsText", stats);
        SetObject(selectionInfo, "_compactListRoot", legacyListRoot);
        SetObject(selectionInfo, "_compactListTextPrefab", rowPrefab);
        SetObject(selectionInfo, "_unitCardRoot", cardRoot);
        SetObject(selectionInfo, "_unitCardPrefab", cardPrefab);
        SetInteger(selectionInfo, "_maxVisibleUnitCards", MaxSelectionUnitCards);
    }

    /// <summary>
    /// Створює HUD-смугу control groups 1-9 над SelectionInfoPanel. Кожен слот показує номер клавіші
    /// та кількість живих юнітів, збережених під цією клавішею.
    /// </summary>
    private static void BuildControlGroupBar(RectTransform parent, Sprite slotSprite)
    {
        HorizontalLayoutGroup layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = ControlGroupSlotSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ControlGroupSlotUI[] slots = new ControlGroupSlotUI[9];

        for (int i = 0; i < slots.Length; i++)
            slots[i] = CreateControlGroupSlot(parent, i + 1, slotSprite);

        ControlGroupBarUI bar = parent.gameObject.AddComponent<ControlGroupBarUI>();
        SetObjectArray(bar, "_slots", slots);
    }

    private static ControlGroupSlotUI CreateControlGroupSlot(RectTransform parent, int groupNumber, Sprite slotSprite)
    {
        RectTransform slot = CreatePanel("ControlGroup_" + groupNumber, parent, slotSprite, new Color(0.018f, 0.027f, 0.036f, 0.95f));
        slot.sizeDelta = new Vector2(ControlGroupSlotWidth, ControlGroupSlotHeight);

        Image slotImage = slot.GetComponent<Image>();
        if (slotImage != null)
            slotImage.raycastTarget = false;

        LayoutElement layout = slot.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = ControlGroupSlotWidth;
        layout.preferredWidth = ControlGroupSlotWidth;
        layout.minHeight = ControlGroupSlotHeight;
        layout.preferredHeight = ControlGroupSlotHeight;

        Outline outline = slot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 1f, 1f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
        AddBorder(slot, new Color(0.9f, 1f, 1f, 0.8f), 1.5f);

        RectTransform activeFrame = CreatePanel("ActiveFrame", slot, null, new Color(0f, 0f, 0f, 0f));
        Stretch(activeFrame, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        AddBorder(activeFrame, new Color(0.05f, 0.75f, 1f, 0.98f), 2f);

        RectTransform iconFrame = CreatePanel("IconFrame", slot, null, new Color(0.03f, 0.05f, 0.07f, 0.96f));
        SetAnchor(
            iconFrame,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 5f),
            new Vector2(32f, 22f));
        AddBorder(iconFrame, new Color(0.16f, 0.42f, 0.58f, 0.86f), 1f);

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        iconObject.transform.SetParent(iconFrame, false);
        RectTransform iconRect = (RectTransform)iconObject.transform;
        Stretch(iconRect, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        Image icon = iconObject.GetComponent<Image>();
        icon.enabled = false;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text fallback = CreateText("Fallback", iconFrame, "UNIT", 10f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(fallback.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        fallback.color = new Color(0.86f, 0.97f, 1f, 1f);
        fallback.gameObject.SetActive(false);

        RectTransform keyBadge = CreatePanel("KeyBadge", slot, null, new Color(0.01f, 0.05f, 0.08f, 0.98f));
        SetAnchor(
            keyBadge,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(4f, 4f),
            new Vector2(20f, 17f));
        AddBorder(keyBadge, new Color(0.9f, 1f, 1f, 0.78f), 1f);

        TMP_Text number = CreateText("Number", keyBadge, groupNumber.ToString(), 14f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(number.rectTransform, Vector2.zero, Vector2.zero);
        number.color = Color.white;

        RectTransform countBadge = CreatePanel("CountBadge", slot, null, new Color(0.02f, 0.46f, 0.85f, 1f));
        SetAnchor(
            countBadge,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-4f, 4f),
            new Vector2(30f, 18f));
        AddBorder(countBadge, new Color(0.88f, 0.98f, 1f, 0.94f), 1f);

        TMP_Text count = CreateText("Count", countBadge, string.Empty, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(count.rectTransform, Vector2.zero, Vector2.zero);
        count.color = Color.white;
        count.gameObject.SetActive(false);

        ControlGroupSlotUI slotUi = slot.gameObject.AddComponent<ControlGroupSlotUI>();
        SetObject(slotUi, "_numberText", number);
        SetObject(slotUi, "_countText", count);
        SetObject(slotUi, "_iconImage", icon);
        SetObject(slotUi, "_fallbackText", fallback);
        SetObject(slotUi, "_background", slot.GetComponent<Image>());
        SetObject(slotUi, "_activeFrame", activeFrame.GetComponent<Image>());
        slot.gameObject.SetActive(false);
        return slotUi;
    }

    /// <summary>
    /// Створює область CommandDeck: смужку вкладок (Build/Units/Outpost) та підпанелі вмісту
    /// для ConstructionPanelUI, ProductionPanelUI і OutpostPanelUI. Повертає посилання на панелі
    /// через структуру CommandPanels для використання в ConfigureUIManager.
    /// </summary>
    private static CommandPanels BuildCommandDeck(RectTransform parent, Sprite buttonSprite)
    {
        RectTransform tabs = CreateRect("CommandTabs", parent, new Vector2(0f, 1f), Vector2.one);
        SetAnchor(tabs, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(-20f, 48f));

        HorizontalLayoutGroup tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 6f;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;

        CreateTabButton(tabs, "Build", PanelType.Construction, false, false, buttonSprite);
        CreateTabButton(tabs, "Units", PanelType.Factory, false, true, buttonSprite);
        CreateTabButton(tabs, "Outpost", PanelType.Outpost, false, false, buttonSprite);

        RectTransform content = CreateRect("CommandContent", parent, Vector2.zero, Vector2.one);
        content.offsetMin = new Vector2(10f, 10f);
        content.offsetMax = new Vector2(-10f, -68f);

        RectTransform idlePanel = CreatePanel("IdlePanel", content, null, new Color(0f, 0f, 0f, 0f));
        Stretch(idlePanel, Vector2.zero, Vector2.zero);

        RectTransform constructionPanel = CreateCommandPanel("ConstructionPanel", content);
        ConstructionPanelUI construction = constructionPanel.gameObject.AddComponent<ConstructionPanelUI>();
        RectTransform constructionContent = CreateScrollContent(constructionPanel, "ConstructionScroll", out TMP_Text constructionEmpty);
        SetObject(construction, "_contentRoot", constructionContent);
        SetObject(construction, "_emptyText", constructionEmpty);
        SetObject(construction, "_placementManager", Object.FindFirstObjectByType<BuildingPlacementManager>());
        SetObject(construction, "_fontAsset", _uiFontAsset);
        SetObject(construction, "_buttonSprite", buttonSprite);
        SetBuildingList(construction, AssetDatabase.LoadAssetAtPath<BuildingData>(HeavyFactoryDataPath));

        RectTransform factoryPanel = CreateCommandPanel("FactoryPanel", content);
        ProductionPanelUI production = factoryPanel.gameObject.AddComponent<ProductionPanelUI>();
        RectTransform productionContent = CreateScrollContent(factoryPanel, "ProductionScroll", out TMP_Text productionEmpty);
        SetObject(production, "_contentRoot", productionContent);
        SetObject(production, "_emptyText", productionEmpty);
        SetObject(production, "_buttonPrefab", AssetDatabase.LoadAssetAtPath<ProductionButtonUI>(ProductionButtonPrefabPath));
        SetObject(production, "_fontAsset", _uiFontAsset);
        SetObject(production, "_buttonSprite", buttonSprite);

        RectTransform outpostPanel = CreateCommandPanel("OutpostPanel", content);
        OutpostPanelUI outpost = outpostPanel.gameObject.AddComponent<OutpostPanelUI>();
        SetObject(outpost, "_fontAsset", _uiFontAsset);
        BuildOutpostPanel(outpostPanel, outpost, buttonSprite);

        constructionPanel.gameObject.SetActive(false);
        factoryPanel.gameObject.SetActive(false);
        outpostPanel.gameObject.SetActive(false);

        return new CommandPanels(idlePanel.gameObject, factoryPanel.gameObject, constructionPanel.gameObject, outpostPanel.gameObject);
    }

    /// <summary>
    /// Створює область вмісту з прокруткою у вигляді сітки та плейсхолдер-текст "порожньо" всередині
    /// <paramref name="parent"/>; виводить порожній ярлик через параметр out.
    /// </summary>
    private static RectTransform CreateScrollContent(RectTransform parent, string objectName, out TMP_Text emptyText)
    {
        RectTransform content = CreateRect(objectName + "Content", parent, Vector2.zero, Vector2.one);
        Stretch(content, Vector2.zero, Vector2.zero);
        content.gameObject.AddComponent<GridLayoutGroup>();
        content.gameObject.AddComponent<ContentSizeFitter>();

        emptyText = CreateText("EmptyText", parent, "Select an item", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(emptyText.rectTransform, Vector2.zero, Vector2.zero);
        emptyText.gameObject.SetActive(false);

        return content;
    }

    /// <summary>
    /// Будує підпанель Outpost з текстовими полями інформації/ресурсів та кнопкою Upgrade,
    /// після чого підключає посилання до <paramref name="outpostPanel"/>.
    /// </summary>
    private static void BuildOutpostPanel(RectTransform parent, OutpostPanelUI outpostPanel, Sprite buttonSprite)
    {
        TMP_Text cost = CreateText("OutpostInfo", parent, "Select a captured outpost", 18f, FontStyles.Bold,
            TextAlignmentOptions.Center);
        SetAnchor(cost.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -20f), new Vector2(-24f, 62f));

        TMP_Text resource = CreateText("OutpostResources", parent, "Resources: 0", 16f, FontStyles.Normal,
            TextAlignmentOptions.Center);
        resource.color = MutedTextColor;
        SetAnchor(resource.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -84f), new Vector2(-24f, 28f));

        Button upgrade = CreateButton("UpgradeButton", parent, "Upgrade", buttonSprite);
        RectTransform upgradeRect = upgrade.transform as RectTransform;
        SetAnchor(upgradeRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 16f), new Vector2(260f, 44f));

        SetObject(outpostPanel, "_upgradeButton", upgrade);
        SetObject(outpostPanel, "_costText", cost);
        SetObject(outpostPanel, "_resourceText", resource);
    }

    /// <summary>
    /// Створює прозорий повністю розтягнутий RectTransform панелі для слоту підпанелі команд.
    /// </summary>
    private static RectTransform CreateCommandPanel(string objectName, Transform parent)
    {
        RectTransform panel = CreatePanel(objectName, parent, null, new Color(0f, 0f, 0f, 0f));
        Stretch(panel, Vector2.zero, Vector2.zero);
        return panel;
    }

    /// <summary>
    /// Створює кнопку вкладки з компонентом HudPanelButton, прив'язаним до заданого PanelType,
    /// та додає LayoutElement з фіксованою мінімальною висотою.
    /// </summary>
    private static Button CreateTabButton(
        Transform parent,
        string label,
        PanelType panel,
        bool requiresBuildArea,
        bool requiresFactory,
        Sprite sprite)
    {
        Button button = CreateButton(label + "Tab", parent, label, sprite);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 48f;
        layout.preferredHeight = 48f;
        layout.minWidth = 96f;

        HudPanelButton hudButton = button.gameObject.AddComponent<HudPanelButton>();
        SetObject(hudButton, "_button", button);
        SetInt(hudButton, "_targetPanel", (int)panel);
        SetBool(hudButton, "_requiresConstructionCenter", requiresBuildArea);
        SetBool(hudButton, "_requiresFactory", requiresFactory);
        return button;
    }

    /// <summary>
    /// Створює GameObject UI-кнопки з дочірнім текстом-ярликом, стилізованим кольором ButtonColor
    /// та необов'язковим фоном у вигляді нарізаного спрайту.
    /// </summary>
    private static Button CreateButton(string objectName, Transform parent, string label, Sprite sprite)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText("Label", buttonObject.transform, label, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.color = ButtonTextColor;
        Stretch(text.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
        return button;
    }

    /// <summary>
    /// Створює панель RectTransform з компонентом Image, використовуючи заданий спрайт та колір.
    /// </summary>
    private static RectTransform CreatePanel(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject panelObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        panelObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)panelObject.transform;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = true;

        return rect;
    }

    /// <summary>
    /// Створює простий RectTransform GameObject (без Image) із заданими межами якоря.
    /// </summary>
    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)rectObject.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    /// <summary>
    /// Створює GameObject TextMeshProUGUI із шрифтом проекту, авто-масштабуванням та кольором тексту;
    /// повертає посилання на TMP_Text.
    /// </summary>
    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        string text,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        textObject.transform.SetParent(parent, false);

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        if (_uiFontAsset != null)
            label.font = _uiFontAsset;

        label.text = text;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(14f, fontSize);
        label.fontSizeMax = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = TextColor;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    /// <summary>
    /// Заповнює список _panels UIManager чотирма записами панелей командної палуби
    /// (MainMenu/idle, Factory, Construction, Outpost) через SerializedObject.
    /// </summary>
    private static void ConfigureUIManager(UIManager manager, CommandPanels panels)
    {
        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty panelList = serialized.FindProperty("_panels");

        if (panelList == null)
            return;

        panelList.arraySize = 4;
        SetPanelEntry(panelList.GetArrayElementAtIndex(0), PanelType.MainMenu, panels.IdlePanel);
        SetPanelEntry(panelList.GetArrayElementAtIndex(1), PanelType.Factory, panels.FactoryPanel);
        SetPanelEntry(panelList.GetArrayElementAtIndex(2), PanelType.Construction, panels.ConstructionPanel);
        SetPanelEntry(panelList.GetArrayElementAtIndex(3), PanelType.Outpost, panels.OutpostPanel);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    /// <summary>
    /// Встановлює enum PanelType та посилання на GameObject панелі в одному записі панелі UIManager.
    /// Підтримує обидві угоди щодо іменування властивостей: "_type"/"_panelObject" та "type"/"panelObject".
    /// </summary>
    private static void SetPanelEntry(SerializedProperty element, PanelType type, GameObject panel)
    {
        SerializedProperty typeProperty = element.FindPropertyRelative("_type") ??
                                          element.FindPropertyRelative("type");
        SerializedProperty objectProperty = element.FindPropertyRelative("_panelObject") ??
                                            element.FindPropertyRelative("panelObject");

        if (typeProperty != null)
            typeProperty.enumValueIndex = (int)type;

        if (objectProperty != null)
            objectProperty.objectReferenceValue = panel;
    }

    /// <summary>
    /// Записує масив _buildings у ConstructionPanelUI з одним записом BuildingData
    /// (або очищає його, якщо building є null).
    /// </summary>
    private static void SetBuildingList(ConstructionPanelUI target, BuildingData building)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty buildings = serialized.FindProperty("_buildings");

        if (buildings == null)
            return;

        buildings.arraySize = building != null ? 1 : 0;

        if (building != null)
            buildings.GetArrayElementAtIndex(0).objectReferenceValue = building;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    /// <summary>
    /// Перевіряє, що UIManager має щонайменше чотири записи панелей, що охоплюють усі необхідні PanelType,
    /// та що кожне посилання на об'єкт панелі призначене.
    /// </summary>
    private static void ValidatePanelList(UIManager manager, List<string> errors)
    {
        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty panels = serialized.FindProperty("_panels");

        if (panels == null || panels.arraySize < 4)
        {
            errors.Add("UIManager should have command deck panel entries.");
            return;
        }

        HashSet<int> found = new HashSet<int>();
        for (int i = 0; i < panels.arraySize; i++)
        {
            SerializedProperty element = panels.GetArrayElementAtIndex(i);
            SerializedProperty type = element.FindPropertyRelative("_type") ??
                                      element.FindPropertyRelative("type");
            SerializedProperty panelObject = element.FindPropertyRelative("_panelObject") ??
                                             element.FindPropertyRelative("panelObject");

            if (type != null)
                found.Add(type.enumValueIndex);

            if (panelObject == null || panelObject.objectReferenceValue == null)
                errors.Add("UIManager has an empty panel reference.");
        }

        if (!found.Contains((int)PanelType.MainMenu) ||
            !found.Contains((int)PanelType.Factory) ||
            !found.Contains((int)PanelType.Construction) ||
            !found.Contains((int)PanelType.Outpost))
        {
            errors.Add("UIManager command panels should include MainMenu, Factory, Construction, and Outpost.");
        }
    }

    /// <summary>
    /// Перевіряє панель TopResources: розмір TMP-тексту, розміри RectTransform панелі
    /// та наявність рівно одного OutpostStatusUI на цьому об'єкті.
    /// </summary>
    private static void ValidateResourceHud(List<string> errors)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TopResourcesPrefabPath) == null)
            errors.Add("TopResources prefab should exist at " + TopResourcesPrefabPath + ".");

        GameObject topResources = GameObject.Find("TopResources");
        if (topResources == null)
            return;

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(topResources);
        if (prefabPath != TopResourcesPrefabPath)
            errors.Add("TopResources scene object should be an instance of " + TopResourcesPrefabPath + ".");

        HorizontalLayoutGroup resourceLayout = topResources.GetComponent<HorizontalLayoutGroup>();
        if (resourceLayout == null)
        {
            errors.Add("TopResources prefab should use HorizontalLayoutGroup configured in Unity.");
        }
        else
        {
            if (resourceLayout.padding.left != resourceLayout.padding.right)
                errors.Add("TopResources horizontal padding should be symmetrical.");

            ValidateTopResourcesMinimumWidth(topResources.transform, resourceLayout, errors);
        }

        RectTransform resourceRect = topResources.GetComponent<RectTransform>();
        if (resourceRect == null)
        {
            errors.Add("TopResources should have RectTransform.");
        }
        else
        {
            if (resourceRect.anchorMin != new Vector2(0f, 1f) ||
                resourceRect.anchorMax != new Vector2(0f, 1f) ||
                resourceRect.pivot != new Vector2(0f, 1f))
            {
                errors.Add("TopResources should be anchored to the upper-left corner.");
            }

            if (resourceRect.sizeDelta.x < 560f || resourceRect.sizeDelta.x > 720f || resourceRect.sizeDelta.y > 64f)
                errors.Add("TopResources background should be compact and avoid empty space.");
        }

        OutpostStatusUI[] statuses = Object.FindObjectsByType<OutpostStatusUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int topStatusCount = 0;
        OutpostStatusUI topStatus = null;
        for (int i = 0; i < statuses.Length; i++)
        {
            if (statuses[i] == null)
                continue;

            if (statuses[i].gameObject.name == "TopResources")
            {
                topStatusCount++;
                topStatus = statuses[i];
            }
            else
                errors.Add("Legacy OutpostStatusUI should be removed from " + statuses[i].gameObject.name + ".");
        }

        if (topStatusCount != 1)
            errors.Add("There should be exactly one OutpostStatusUI on TopResources.");

        if (topStatus != null)
        {
            SerializedObject serialized = new SerializedObject(topStatus);
            ValidateObjectReference(serialized, "_zonesValueText", errors, "TopResources should reference ZonesValue.");
            ValidateObjectReference(serialized, "_moneyValueText", errors, "TopResources should reference MoneyValue.");
            ValidateObjectReference(serialized, "_incomeValueText", errors, "TopResources should reference IncomeValue.");
        }
    }

    /// <summary>Перевіряє prefab-налаштування LayoutElement без зміни UI-структури.</summary>
    private static void ValidateTopResourcesMinimumWidth(
        Transform parent,
        HorizontalLayoutGroup layout,
        List<string> errors)
    {
        const float narrowViewportWidth = 320f;
        const float narrowOuterMargin = 20f;
        float availableWidth = narrowViewportWidth - narrowOuterMargin;
        float width = layout.padding.left + layout.padding.right;
        int visibleChildCount = 0;

        foreach (Transform child in parent)
        {
            if (!child.gameObject.activeSelf)
                continue;

            LayoutElement element = child.GetComponent<LayoutElement>();
            if (element == null || element.ignoreLayout)
                continue;

            width += Mathf.Max(0f, element.minWidth);
            visibleChildCount++;
        }

        if (visibleChildCount > 1)
            width += layout.spacing * (visibleChildCount - 1);

        if (width > availableWidth)
            errors.Add("TopResources prefab minimum layout width should fit a 320px-wide viewport.");
    }

    /// <summary>
    /// Перевіряє, що рамка BottomHud є повноширинною, прикріпленою до нижньої частини екрана,
    /// достатньо високою для своїх дочірніх елементів, а MinimapSlot/SelectionInfoPanel/CommandDeck
    /// візуально знаходяться всередині меж рамки.
    /// </summary>
    private static void ValidateBottomHudFrame(List<string> errors)
    {
        GameObject bottomObject = GameObject.Find("BottomHud");
        RectTransform bottom = bottomObject != null ? bottomObject.GetComponent<RectTransform>() : null;
        if (bottom == null)
            return;

        if (Mathf.Abs(bottom.sizeDelta.x) > 0.1f)
            errors.Add("BottomHud frame should stay inside the screen horizontally.");

        if (Mathf.Abs(bottom.anchoredPosition.y) > 0.1f)
            errors.Add("BottomHud frame should stay inside the screen vertically.");

        if (bottom.sizeDelta.y < 300f)
            errors.Add("BottomHud frame should be taller than the child panels so they sit inside the frame.");

        ValidateChildInsideBottom(bottom, "MinimapSlot", errors);
        ValidateChildInsideBottom(bottom, "SelectionInfoPanel", errors);
        ValidateChildInsideBottom(bottom, "ControlGroupBar", errors, 1f);
        ValidateChildInsideBottom(bottom, "CommandDeck", errors);
    }

    /// <summary>
    /// Використовує кути у світовому просторі для перевірки, що <paramref name="childName"/> повністю
    /// міститься всередині <paramref name="bottom"/> з відступом не менше 8 одиниць.
    /// </summary>
    private static void ValidateChildInsideBottom(RectTransform bottom, string childName, List<string> errors, float margin = 8f)
    {
        GameObject childObject = GameObject.Find(childName);
        RectTransform child = childObject != null ? childObject.GetComponent<RectTransform>() : null;
        if (bottom == null || child == null)
            return;

        Vector3[] bottomCorners = new Vector3[4];
        Vector3[] childCorners = new Vector3[4];
        bottom.GetWorldCorners(bottomCorners);
        child.GetWorldCorners(childCorners);

        bool contained =
            childCorners[0].x >= bottomCorners[0].x + margin &&
            childCorners[0].y >= bottomCorners[0].y + margin &&
            childCorners[2].x <= bottomCorners[2].x - margin &&
            childCorners[2].y <= bottomCorners[2].y - margin;

        if (!contained)
            errors.Add(childName + " should be fully inside BottomHud background.");
    }

    /// <summary>
    /// Перевіряє префаб ProductionButton на наявність необхідних компонентів, непрозорого фону,
    /// видимого контуру та всіх призначених серіалізованих посилань на UI-поля.
    /// </summary>
    private static void ValidateProductionButtonPrefab(List<string> errors)
    {
        ProductionButtonUI prefab = AssetDatabase.LoadAssetAtPath<ProductionButtonUI>(ProductionButtonPrefabPath);
        if (prefab == null)
        {
            errors.Add("ProductionButtonPrefab should have ProductionButtonUI.");
            return;
        }

        Image background = prefab.GetComponent<Image>();
        Button button = prefab.GetComponent<Button>();
        if (background == null || button == null)
            errors.Add("ProductionButtonPrefab should have Image and Button components.");
        else if (background.color.a < 0.95f || background.sprite != null)
            errors.Add("ProductionButtonPrefab should use a visible solid background image.");

        if (prefab.GetComponent<Outline>() == null)
            errors.Add("ProductionButtonPrefab should have a visible outline.");

        SerializedObject serialized = new SerializedObject(prefab);
        ValidateObjectReference(serialized, "_nameText", errors, "ProductionButtonPrefab should reference NameText.");
        ValidateObjectReference(serialized, "_costText", errors, "ProductionButtonPrefab should reference CostText.");
        ValidateObjectReference(serialized, "_timeText", errors, "ProductionButtonPrefab should reference TimeText.");
        ValidateObjectReference(serialized, "_fallbackText", errors, "ProductionButtonPrefab should reference FallbackIcon text.");

        SerializedProperty iconProperty = serialized.FindProperty("_icon");
        if (iconProperty == null || iconProperty.objectReferenceValue == null)
        {
            errors.Add("ProductionButtonPrefab should reference a child Icon image.");
            return;
        }

        if (background != null && iconProperty.objectReferenceValue == background)
            errors.Add("ProductionButtonPrefab _icon must not reference the root background image.");
    }

    /// <summary>
    /// Перевіряє prefab selection-картки на наявність кореневого Image/LayoutElement та всіх UI-посилань.
    /// </summary>
    private static void ValidateSelectionUnitCardPrefab(List<string> errors)
    {
        SelectionUnitCardUI prefab = AssetDatabase.LoadAssetAtPath<SelectionUnitCardUI>(SelectionUnitCardPrefabPath);
        if (prefab == null)
        {
            errors.Add("SelectionUnitCard prefab should have SelectionUnitCardUI.");
            return;
        }

        if (prefab.GetComponent<Image>() == null)
            errors.Add("SelectionUnitCard prefab should have Image background.");

        if (prefab.GetComponent<LayoutElement>() == null)
            errors.Add("SelectionUnitCard prefab should have LayoutElement.");

        SerializedObject serialized = new SerializedObject(prefab);
        ValidateObjectReference(serialized, "_iconImage", errors, "SelectionUnitCard should reference Icon image.");
        ValidateObjectReference(serialized, "_fallbackText", errors, "SelectionUnitCard should reference Fallback text.");
        ValidateObjectReference(serialized, "_countText", errors, "SelectionUnitCard should reference Count text.");
        ValidateObjectReference(serialized, "_nameText", errors, "SelectionUnitCard should reference Name text.");
    }

    /// <summary>
    /// Перевіряє, що іменоване серіалізоване посилання на Object призначене; додає <paramref name="message"/> при невдачі.
    /// </summary>
    private static void ValidateObjectReference(
        SerializedObject serialized,
        string propertyName,
        List<string> errors,
        string message)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
            errors.Add(message);
    }

    /// <summary>
    /// Перевіряє, що іменований GameObject існує в сцені; додає помилку про відсутній об'єкт, якщо ні.
    /// </summary>
    private static void ValidateObject(string objectName, List<string> errors)
    {
        if (GameObject.Find(objectName) == null)
            errors.Add(objectName + " is missing.");
    }

    /// <summary>
    /// Додає чотири дочірніх Image-рядки рамки (зверху, знизу, зліва, справа) до <paramref name="target"/>
    /// із заданим кольором та товщиною.
    /// </summary>
    private static void AddBorder(RectTransform target, Color color, float thickness)
    {
        if (target == null)
            return;

        CreateBorderLine(target, "BorderTop", color, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, thickness));
        CreateBorderLine(target, "BorderBottom", color, Vector2.zero, new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, thickness));
        CreateBorderLine(target, "BorderLeft", color, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(thickness, 0f));
        CreateBorderLine(target, "BorderRight", color, new Vector2(1f, 0f), Vector2.one,
            new Vector2(1f, 0.5f), Vector2.zero, new Vector2(thickness, 0f));
    }

    /// <summary>
    /// Створює один тонкий дочірній Image, що використовується як один бік декоративної рамки.
    /// </summary>
    private static void CreateBorderLine(
        RectTransform parent,
        string objectName,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject line = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        line.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)line.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = line.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    /// <summary>
    /// Розтягує RectTransform для повного заповнення батьківського об'єкта з необов'язковими відступами.
    /// </summary>
    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    /// <summary>
    /// Встановлює всі властивості якоря, pivot, позиції та розміру RectTransform за один виклик.
    /// </summary>
    private static void SetAnchor(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    /// <summary>
    /// Завантажує Sprite зі шляху <paramref name="path"/>; повертається до сканування всіх підасетів,
    /// якщо основне завантаження повертає null (наприклад, для PSD-файлів із кількома фрагментами).
    /// </summary>
    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite != null)
            return sprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite nestedSprite)
                return nestedSprite;
        }

        return null;
    }

    /// <summary>
    /// Завантажує Jupiter TMP_FontAsset, якщо він вже має дійсну текстуру атласу; інакше
    /// видаляє застарілий асет і регенерує його з вихідного TTF у режимі динамічного атласу.
    /// </summary>
    private static TMP_FontAsset LoadOrCreateFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JupiterTmpFontPath);
        if (existing != null && existing.atlasTextures != null &&
            existing.atlasTextures.Length > 0 &&
            existing.atlasTextures[0] != null)
        {
            return existing;
        }

        if (existing != null)
            AssetDatabase.DeleteAsset(JupiterTmpFontPath);

        Font font = AssetDatabase.LoadAssetAtPath<Font>(JupiterFontPath);
        if (font == null)
            return null;

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);
        if (fontAsset == null)
            return null;

        AssetDatabase.CreateAsset(fontAsset, JupiterTmpFontPath);

        if (fontAsset.material != null)
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        if (fontAsset.atlasTextures != null &&
            fontAsset.atlasTextures.Length > 0 &&
            fontAsset.atlasTextures[0] != null)
        {
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(JupiterTmpFontPath);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JupiterTmpFontPath);
    }

    /// <summary>
    /// Встановлює серіалізовану властивість-посилання на Object у <paramref name="target"/> та негайно застосовує.
    /// </summary>
    private static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Встановлює серіалізований масив Object у <paramref name="target"/> за назвою властивості.
    /// </summary>
    private static void SetObjectArray(Object target, string propertyName, Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null || !property.isArray)
            return;

        property.arraySize = values != null ? values.Length : 0;

        if (values != null)
        {
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    /// <summary>
    /// Встановлює серіалізовану властивість-enum (int) у <paramref name="target"/> та негайно застосовує.
    /// </summary>
    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.enumValueIndex = value;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Встановлює серіалізовану властивість типу int у <paramref name="target"/> та негайно застосовує.
    /// </summary>
    private static void SetInteger(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.intValue = value;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Встановлює серіалізовану властивість типу bool у <paramref name="target"/> та негайно застосовує.
    /// </summary>
    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.boolValue = value;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Тип-значення, що групує чотири GameObject підпанелей командної палуби, повернутих BuildCommandDeck.</summary>
    private readonly struct CommandPanels
    {
        public CommandPanels(GameObject idlePanel, GameObject factoryPanel, GameObject constructionPanel, GameObject outpostPanel)
        {
            IdlePanel = idlePanel;
            FactoryPanel = factoryPanel;
            ConstructionPanel = constructionPanel;
            OutpostPanel = outpostPanel;
        }

        public GameObject IdlePanel { get; }
        public GameObject FactoryPanel { get; }
        public GameObject ConstructionPanel { get; }
        public GameObject OutpostPanel { get; }
    }
}
#endif
