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
public static class RtsHudBuilder
{
    private const string MainScenePath = "Assets/Scenes/mainScene.unity";
    private const string HeavyFactoryDataPath = "Assets/Balance/HeavyFactory.asset";
    private const string ProductionButtonPrefabPath = "Assets/Prefabs/ProductionButtonPrefab.prefab";
    private const string SfWindowPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Window.psd";
    private const string SfGenericPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Generic.psd";
    private const string SfButtonPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Button.psd";
    private const string JupiterFontPath = "Assets/Unity UI Samples/Fonts/Jupiter/Jupiter.ttf";
    private const string JupiterTmpFontPath = "Assets/Unity UI Samples/Fonts/Jupiter/Jupiter TMP.asset";

    private static readonly Color PanelColor = Color.white;
    private static readonly Color PanelColorStrong = Color.white;
    private static readonly Color AccentColor = new Color(0.03f, 0.38f, 0.75f, 1f);
    private static readonly Color TextColor = new Color(0.04f, 0.075f, 0.11f, 1f);
    private static readonly Color MutedTextColor = new Color(0.22f, 0.34f, 0.44f, 1f);
    private static readonly Color ButtonColor = new Color(0f, 0.55f, 1f, 1f);
    private static readonly Color ButtonTextColor = Color.white;

    private static TMP_FontAsset _uiFontAsset;

    [MenuItem("Tools/RTS/Build RTS HUD")]
    public static void Build()
    {
        _uiFontAsset = LoadOrCreateFontAsset();

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Canvas canvas = EnsureCanvas();
        EnsureEventSystem();
        ClearCanvas(canvas);
        RemoveLegacyResourceStatus();

        Sprite windowSprite = LoadSprite(SfWindowPath);
        Sprite genericSprite = LoadSprite(SfGenericPath);
        Sprite buttonSprite = LoadSprite(SfButtonPath);
        RepairProductionButtonPrefab(buttonSprite);

        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform hudRoot = CreateRect("RtsHudRoot", canvas.transform, Vector2.zero, Vector2.one);
        hudRoot.offsetMin = Vector2.zero;
        hudRoot.offsetMax = Vector2.zero;

        RectTransform topResources = CreatePanel("TopResources", hudRoot, windowSprite, PanelColorStrong);
        TMP_Text resourceText = CreateText("ResourceText", topResources, "RESOURCES", 28f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        resourceText.richText = true;
        resourceText.fontSizeMin = 18f;
        resourceText.fontSizeMax = 28f;
        resourceText.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(resourceText.rectTransform, new Vector2(24f, 10f), new Vector2(-22f, -10f));
        OutpostStatusUI statusUI = topResources.gameObject.AddComponent<OutpostStatusUI>();
        SetObject(statusUI, "_canvas", canvas);
        SetObject(statusUI, "_statusText", resourceText);
        SetObject(statusUI, "_fontAsset", _uiFontAsset);

        RectTransform bottomHud = CreatePanel("BottomHud", hudRoot, windowSprite, PanelColorStrong);

        RectTransform minimap = CreatePanel("MinimapSlot", bottomHud, genericSprite, PanelColor);
        TMP_Text mapTitle = CreateText("MapTitle", minimap, "MAP", 20f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(mapTitle.rectTransform, new Vector2(10f, 54f), new Vector2(-10f, -42f));
        TMP_Text mapHint = CreateText("MapHint", minimap, "minimap reserved", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(mapHint.rectTransform, new Vector2(8f, 20f), new Vector2(-8f, -76f));

        RectTransform infoPanel = CreatePanel("SelectionInfoPanel", bottomHud, genericSprite, PanelColor);
        BuildSelectionInfo(infoPanel);

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
        SetObject(layout, "_commandDeck", commandDeck);
        SetObject(layout, "_topResources", topResources);
        layout.ApplyLayout();

        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("RTS HUD rebuilt in mainScene.");
    }

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
        ValidateObject("CommandDeck", errors);
        ValidateResourceHud(errors);
        ValidateBottomHudFrame(errors);
        ValidateProductionButtonPrefab(errors);

        if (Object.FindFirstObjectByType<RtsHudResponsiveLayout>() == null)
            errors.Add("RtsHudResponsiveLayout is missing.");

        if (Object.FindFirstObjectByType<SelectionInfoPanelUI>() == null)
            errors.Add("SelectionInfoPanelUI is missing.");

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

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    private static void ClearCanvas(Canvas canvas)
    {
        List<GameObject> children = new List<GameObject>();

        foreach (Transform child in canvas.transform)
            children.Add(child.gameObject);

        for (int i = 0; i < children.Count; i++)
            Object.DestroyImmediate(children[i]);
    }

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

    private static void RepairProductionButtonPrefab(Sprite buttonSprite)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ProductionButtonPrefabPath);
        if (root == null)
            return;

        try
        {
            RectTransform rect = root.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(116f, 108f);
            }

            Image background = EnsureComponent<Image>(root);
            background.color = ButtonColor;
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.raycastTarget = true;

            Outline outline = EnsureComponent<Outline>(root);
            outline.effectColor = new Color(1f, 1f, 1f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = EnsureComponent<Button>(root);
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.pressedColor = Color.white;
            colors.selectedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.78f);
            button.colors = colors;

            Image icon = EnsureChildImage(root.transform, "Icon");
            ConfigureChildRect(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(54f, 34f));
            icon.sprite = null;
            icon.enabled = false;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = Color.white;

            TMP_Text fallback = EnsureChildText(root.transform, "FallbackIcon", null);
            ConfigureChildRect(fallback.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(82f, 32f));
            ConfigurePrefabText(fallback, "UNIT", 20f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            TMP_Text name = EnsureChildText(root.transform, "NameText", "Text (TMP)");
            ConfigureChildRect(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(106f, 30f));
            ConfigurePrefabText(name, "Unit", 16f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            TMP_Text cost = EnsureChildText(root.transform, "CostText", null);
            ConfigureChildRect(cost.rectTransform, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(9f, 8f), new Vector2(48f, 21f));
            ConfigurePrefabText(cost, "$0", 14f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);

            TMP_Text time = EnsureChildText(root.transform, "TimeText", null);
            ConfigureChildRect(time.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-9f, 8f), new Vector2(50f, 21f));
            ConfigurePrefabText(time, "0s", 14f, FontStyles.Normal, TextAlignmentOptions.Right, Color.white);

            ProductionButtonUI productionButton = EnsureComponent<ProductionButtonUI>(root);
            SetObject(productionButton, "_icon", icon);
            SetObject(productionButton, "_fallbackText", fallback);
            SetObject(productionButton, "_nameText", name);
            SetObject(productionButton, "_costText", cost);
            SetObject(productionButton, "_timeText", time);
            SetObject(productionButton, "_button", button);
            SetObject(productionButton, "_fontAsset", _uiFontAsset);
            SetObject(productionButton, "_buttonSprite", buttonSprite);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, ProductionButtonPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Image EnsureChildImage(Transform parent, string objectName)
    {
        Transform child = parent.Find(objectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            childObject.transform.SetParent(parent, false);
            return childObject.GetComponent<Image>();
        }

        Image image = child.GetComponent<Image>();
        return image != null ? image : child.gameObject.AddComponent<Image>();
    }

    private static TMP_Text EnsureChildText(Transform parent, string objectName, string legacyName)
    {
        Transform child = parent.Find(objectName);
        if (child == null && !string.IsNullOrEmpty(legacyName))
        {
            child = parent.Find(legacyName);
            if (child != null)
                child.name = objectName;
        }

        if (child == null)
        {
            GameObject childObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            childObject.transform.SetParent(parent, false);
            return childObject.GetComponent<TMP_Text>();
        }

        TMP_Text text = child.GetComponent<TMP_Text>();
        return text != null ? text : child.gameObject.AddComponent<TextMeshProUGUI>();
    }

    private static void ConfigureChildRect(
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

    private static void ConfigurePrefabText(
        TMP_Text text,
        string value,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        if (text == null)
            return;

        if (_uiFontAsset != null)
            text.font = _uiFontAsset;

        text.text = value;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize <= 14f ? 12f : 14f;
        text.fontSizeMax = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void BuildSelectionInfo(RectTransform parent)
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
        SetAnchor(stats.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(0f, 0f),
            new Vector2(16f, 14f), new Vector2(-20f, -74f));

        RectTransform listRoot = CreateRect("SelectionCompactList", parent, new Vector2(0.62f, 0f), Vector2.one);
        listRoot.offsetMin = new Vector2(8f, 12f);
        listRoot.offsetMax = new Vector2(-14f, -74f);
        VerticalLayoutGroup listLayout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 2f;
        listLayout.childControlHeight = false;
        listLayout.childControlWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childForceExpandWidth = true;

        TMP_Text rowPrefab = CreateText("CompactRowPrefab", listRoot, "Unit", 14f, FontStyles.Normal, TextAlignmentOptions.Left);
        rowPrefab.gameObject.SetActive(false);
        LayoutElement rowLayout = rowPrefab.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 22f;

        SelectionInfoPanelUI selectionInfo = parent.gameObject.AddComponent<SelectionInfoPanelUI>();
        SetObject(selectionInfo, "_titleText", title);
        SetObject(selectionInfo, "_subtitleText", subtitle);
        SetObject(selectionInfo, "_statsText", stats);
        SetObject(selectionInfo, "_compactListRoot", listRoot);
        SetObject(selectionInfo, "_compactListTextPrefab", rowPrefab);
    }

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

    private static RectTransform CreateCommandPanel(string objectName, Transform parent)
    {
        RectTransform panel = CreatePanel(objectName, parent, null, new Color(0f, 0f, 0f, 0f));
        Stretch(panel, Vector2.zero, Vector2.zero);
        return panel;
    }

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

    private static void ValidateResourceHud(List<string> errors)
    {
        GameObject topResources = GameObject.Find("TopResources");
        if (topResources == null)
            return;

        TMP_Text resourceText = topResources.GetComponentInChildren<TMP_Text>(true);
        if (resourceText == null)
            errors.Add("TopResources should contain resource TMP text.");
        else if (resourceText.fontSizeMax < 28f || resourceText.fontSizeMin < 18f)
            errors.Add("TopResources text should be large enough for readable resource stats.");

        RectTransform resourceRect = topResources.GetComponent<RectTransform>();
        if (resourceRect == null || resourceRect.sizeDelta.x < 1000f || resourceRect.sizeDelta.y < 80f)
            errors.Add("TopResources background should be large enough for one-line resource stats.");

        OutpostStatusUI[] statuses = Object.FindObjectsByType<OutpostStatusUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int topStatusCount = 0;
        for (int i = 0; i < statuses.Length; i++)
        {
            if (statuses[i] == null)
                continue;

            if (statuses[i].gameObject.name == "TopResources")
                topStatusCount++;
            else
                errors.Add("Legacy OutpostStatusUI should be removed from " + statuses[i].gameObject.name + ".");
        }

        if (topStatusCount != 1)
            errors.Add("There should be exactly one OutpostStatusUI on TopResources.");
    }

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
        ValidateChildInsideBottom(bottom, "CommandDeck", errors);
    }

    private static void ValidateChildInsideBottom(RectTransform bottom, string childName, List<string> errors)
    {
        GameObject childObject = GameObject.Find(childName);
        RectTransform child = childObject != null ? childObject.GetComponent<RectTransform>() : null;
        if (bottom == null || child == null)
            return;

        Vector3[] bottomCorners = new Vector3[4];
        Vector3[] childCorners = new Vector3[4];
        bottom.GetWorldCorners(bottomCorners);
        child.GetWorldCorners(childCorners);

        const float margin = 8f;
        bool contained =
            childCorners[0].x >= bottomCorners[0].x + margin &&
            childCorners[0].y >= bottomCorners[0].y + margin &&
            childCorners[2].x <= bottomCorners[2].x - margin &&
            childCorners[2].y <= bottomCorners[2].y - margin;

        if (!contained)
            errors.Add(childName + " should be fully inside BottomHud background.");
    }

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

    private static void ValidateObject(string objectName, List<string> errors)
    {
        if (GameObject.Find(objectName) == null)
            errors.Add(objectName + " is missing.");
    }

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

    private static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.enumValueIndex = value;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.boolValue = value;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

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
