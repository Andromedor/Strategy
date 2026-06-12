using Strategy.Data;
using Strategy.Maps;
using Strategy.Save;
using Strategy.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InGamePauseMenuSetupBuilder
{
    private const string RegistryPath = "Assets/ScriptableObjects/Registries/GameAssetRegistry.asset";
    private const string MapCatalogPath = "Assets/ScriptableObjects/Maps/MapCatalog.asset";
    private const string GridConfigPath = "Assets/Balance/BuildingPlacementGridConfig.asset";
    private const string JupiterFontPath = "Assets/Unity UI Samples/Fonts/Jupiter/Jupiter TMP.asset";

    private static readonly string[] GameplayScenes =
    {
        "Assets/Scenes/mainScene.unity",
        "Assets/Scenes/PrototypeMap_1_2Spawns.unity",
        "Assets/Scenes/PrototypeMap_2_3Spawns.unity",
        "Assets/Scenes/PrototypeMap_3_4Spawns.unity"
    };

    private static TMP_FontAsset _font;

    [MenuItem("Strategy/Setup/In-Game Pause Menu")]
    public static void Apply()
    {
        AssetDatabase.Refresh();
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JupiterFontPath);

        GameAssetRegistry registry = LoadRequired<GameAssetRegistry>(RegistryPath);
        MapCatalog mapCatalog = LoadRequired<MapCatalog>(MapCatalogPath);
        BuildingPlacementGridConfig gridConfig = LoadRequired<BuildingPlacementGridConfig>(GridConfigPath);

        foreach (string scenePath in GameplayScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
                canvas = CreateCanvas();

            EnsureEventSystem();

            SaveGameManager saveManager = EnsureSaveGameManager(registry, mapCatalog, gridConfig);
            InGamePauseMenuUI pauseMenu = EnsurePauseMenu(canvas.transform);
            WirePauseMenu(pauseMenu, saveManager, mapCatalog);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("InGamePauseMenuSetupBuilder: pause menu and save dependencies configured.");
    }

    private static SaveGameManager EnsureSaveGameManager(
        GameAssetRegistry registry,
        MapCatalog mapCatalog,
        BuildingPlacementGridConfig gridConfig)
    {
        SaveGameManager saveManager = Object.FindFirstObjectByType<SaveGameManager>();
        if (saveManager == null)
        {
            GameObject systems = GameObject.Find("Match Systems") ?? new GameObject("Match Systems");
            saveManager = systems.AddComponent<SaveGameManager>();
        }

        SerializedObject serialized = new(saveManager);
        SetObject(serialized, "_registry", registry);
        SetObject(serialized, "_mapCatalog", mapCatalog);
        SetObject(serialized, "_gridConfig", gridConfig);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return saveManager;
    }

    private static InGamePauseMenuUI EnsurePauseMenu(Transform canvas)
    {
        Transform existing = canvas.Find("InGamePauseMenu");
        GameObject root = existing != null ? existing.gameObject : CreatePanel(canvas, "InGamePauseMenu", new Color(0f, 0f, 0f, 0.58f));
        RectTransform rootRect = (RectTransform)root.transform;
        Stretch(rootRect);

        CanvasGroup group = GetOrAdd<CanvasGroup>(root);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        InGamePauseMenuUI menu = GetOrAdd<InGamePauseMenuUI>(root);

        Transform oldWindow = root.transform.Find("PauseWindow");
        if (oldWindow != null)
            Object.DestroyImmediate(oldWindow.gameObject);

        GameObject window = CreatePanel(root.transform, "PauseWindow", new Color(0.05f, 0.15f, 0.24f, 0.96f));
        RectTransform windowRect = (RectTransform)window.transform;
        Center(windowRect, new Vector2(430f, 420f));
        AddOutline(window, new Color(0.22f, 0.62f, 0.95f, 0.75f));

        GameObject mainPanel = CreateLayoutPanel(window.transform, "MainPanel");
        CreateText(mainPanel.transform, "Title", "ПАУЗА", 28f, FontStyles.Bold, TextAlignmentOptions.Center);
        Button resume = CreateButton(mainPanel.transform, "ResumeButton", "ПРОДОВЖИТИ");
        Button save = CreateButton(mainPanel.transform, "SaveButton", "ЗБЕРЕГТИ");
        Button load = CreateButton(mainPanel.transform, "OpenLoadButton", "ЗАВАНТАЖИТИ");
        Button mainMenu = CreateButton(mainPanel.transform, "MainMenuButton", "ГОЛОВНЕ МЕНЮ");
        Button quit = CreateButton(mainPanel.transform, "QuitButton", "ВИЙТИ З ГРИ");

        GameObject loadPanel = CreateLayoutPanel(window.transform, "LoadPanel");
        loadPanel.SetActive(false);
        CreateText(loadPanel.transform, "LoadTitle", "ЗАВАНТАЖЕННЯ", 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        ScrollRect scroll = CreateSaveScroll(loadPanel.transform, out Transform saveListRoot, out Button rowTemplate);
        Button loadSelected = CreateButton(loadPanel.transform, "LoadSelectedButton", "ЗАВАНТАЖИТИ ОБРАНЕ");
        Button back = CreateButton(loadPanel.transform, "BackFromLoadButton", "НАЗАД");

        TMP_Text status = CreateText(window.transform, "StatusText", string.Empty, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
        RectTransform statusRect = (RectTransform)status.transform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 16f);
        statusRect.sizeDelta = new Vector2(-44f, 34f);

        SerializedObject serialized = new(menu);
        SetObject(serialized, "_rootGroup", group);
        SetObject(serialized, "_windowRect", windowRect);
        SetObject(serialized, "_mainPanel", mainPanel);
        SetObject(serialized, "_loadPanel", loadPanel);
        SetObject(serialized, "_resumeButton", resume);
        SetObject(serialized, "_saveButton", save);
        SetObject(serialized, "_openLoadButton", load);
        SetObject(serialized, "_mainMenuButton", mainMenu);
        SetObject(serialized, "_quitButton", quit);
        SetObject(serialized, "_saveListRoot", saveListRoot);
        SetObject(serialized, "_saveRowTemplate", rowTemplate);
        SetObject(serialized, "_loadSelectedButton", loadSelected);
        SetObject(serialized, "_backFromLoadButton", back);
        SetObject(serialized, "_statusText", status);
        SetBool(serialized, "_pauseTimeScale", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        _ = scroll;
        return menu;
    }

    private static void WirePauseMenu(InGamePauseMenuUI menu, SaveGameManager saveManager, MapCatalog mapCatalog)
    {
        SerializedObject serialized = new(menu);
        SetObject(serialized, "_saveGameManager", saveManager);
        SetObject(serialized, "_mapCatalog", mapCatalog);
        SetString(serialized, "_mainMenuSceneName", "MainMenu");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static GameObject CreateLayoutPanel(Transform parent, string name)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 18f);
        rect.sizeDelta = new Vector2(340f, 318f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return panel;
    }

    private static ScrollRect CreateSaveScroll(Transform parent, out Transform content, out Button rowTemplate)
    {
        GameObject scrollObject = CreatePanel(parent, "SaveScroll", new Color(0.02f, 0.08f, 0.13f, 0.72f));
        RectTransform scrollRectTransform = (RectTransform)scrollObject.transform;
        scrollRectTransform.sizeDelta = new Vector2(330f, 166f);
        AddLayout(scrollObject, 330f, 166f);

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject viewport = CreatePanel(scrollObject.transform, "Viewport", new Color(0f, 0f, 0f, 0f));
        RectTransform viewportRect = (RectTransform)viewport.transform;
        Stretch(viewportRect);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = (RectTransform)contentObject.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        rowTemplate = CreateButton(contentObject.transform, "SaveRowTemplate", "Save");
        AddLayout(rowTemplate.gameObject, 300f, 34f);
        rowTemplate.gameObject.SetActive(false);

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        content = contentObject.transform;
        return scroll;
    }

    private static Button CreateButton(Transform parent, string name, string text)
    {
        GameObject buttonObject = CreatePanel(parent, name, new Color(0.08f, 0.24f, 0.38f, 0.92f));
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.sizeDelta = new Vector2(300f, 42f);
        AddOutline(buttonObject, new Color(0.22f, 0.62f, 0.95f, 0.85f));
        AddLayout(buttonObject, 300f, 42f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();

        TMP_Text label = CreateText(buttonObject.transform, "Label", text, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch((RectTransform)label.transform);
        return button;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        if (_font != null)
            label.font = _font;

        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = fontSize;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        AddLayout(textObject, 300f, 34f);
        return label;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        return panel;
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void AddLayout(GameObject gameObject, float preferredWidth, float preferredHeight)
    {
        LayoutElement layout = GetOrAdd<LayoutElement>(gameObject);
        layout.preferredWidth = preferredWidth;
        layout.preferredHeight = preferredHeight;
    }

    private static void AddOutline(GameObject gameObject, Color color)
    {
        Outline outline = GetOrAdd<Outline>(gameObject);
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1.6f, -1.6f);
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"{serialized.targetObject.name}: missing serialized property {propertyName}");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static T LoadRequired<T>(string path) where T : Object
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            Debug.LogError($"InGamePauseMenuSetupBuilder: missing asset {path}");
        return asset;
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }
}
