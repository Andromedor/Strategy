using System.Collections.Generic;
using Strategy.AI;
using Strategy.Buildings;
using Strategy.Core;
using Strategy.Data;
using Strategy.Maps;
using Strategy.Menu;
using Strategy.Save;
using Strategy.Units;
using Strategy.UI;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameMenuSetupBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string MainScenePath = "Assets/Scenes/mainScene.unity";
    private const string MapFolder = "Assets/ScriptableObjects/Maps";
    private const string RegistryFolder = "Assets/ScriptableObjects/Registries";
    private const string MainMapPath = MapFolder + "/MainSceneMap.asset";
    private const string PrototypeMapOneScenePath = "Assets/Scenes/PrototypeMap_1_2Spawns.unity";
    private const string PrototypeMapTwoScenePath = "Assets/Scenes/PrototypeMap_2_3Spawns.unity";
    private const string PrototypeMapThreeScenePath = "Assets/Scenes/PrototypeMap_3_4Spawns.unity";
    private const string PrototypeMapOnePath = MapFolder + "/PrototypeMap_1.asset";
    private const string PrototypeMapTwoPath = MapFolder + "/PrototypeMap_2.asset";
    private const string PrototypeMapThreePath = MapFolder + "/PrototypeMap_3.asset";
    private const string MapCatalogPath = MapFolder + "/MapCatalog.asset";
    private const string RegistryPath = RegistryFolder + "/GameAssetRegistry.asset";
    private const string MatchResultPanelPrefabPath = "Assets/Prefabs/UI/MatchResultPanel.prefab";
    private const string SfBackgroundPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/Background/SF Background.png";
    private const string SfButtonPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Button.psd";
    private const string SfGenericPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Generic.psd";
    private const string SfWindowPath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Window.psd";
    private const string JupiterFontPath = "Assets/Unity UI Samples/Fonts/Jupiter/Jupiter TMP.asset";
    private const float StartPointGroundYOffset = 0.05f;

    private static Sprite _backgroundSprite;
    private static Sprite _buttonSprite;
    private static Sprite _genericSprite;
    private static Sprite _windowSprite;
    private static TMP_FontAsset _menuFont;

    [MenuItem("Tools/RTS/Apply Game Menu Setup")]
    public static void Apply()
    {
        EnsureFolder("Assets", "ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects", "Maps");
        EnsureFolder("Assets/ScriptableObjects", "Registries");

        MapDefinition prototypeOne = LoadOrCreate<MapDefinition>(PrototypeMapOnePath);
        MapDefinition prototypeTwo = LoadOrCreate<MapDefinition>(PrototypeMapTwoPath);
        MapDefinition prototypeThree = LoadOrCreate<MapDefinition>(PrototypeMapThreePath);
        MapDefinition mainMap = LoadOrCreate<MapDefinition>(MainMapPath);
        ConfigureMap(prototypeOne, "prototype_map_1", "Prototype Map 1 - 2 Spawns", PrototypeMapOneScenePath, 2, new[] { SkirmishTeamMode.OneVsOne });
        ConfigureMap(prototypeTwo, "prototype_map_2", "Prototype Map 2 - 3 Spawns", PrototypeMapTwoScenePath, 3, new[] { SkirmishTeamMode.ThreePlayer });
        ConfigureMap(prototypeThree, "prototype_map_3", "Prototype Map 3 - 4 Spawns", PrototypeMapThreeScenePath, 4, new[] { SkirmishTeamMode.TwoVsTwo });
        ConfigureMainMap(mainMap);
        MapCatalog catalog = LoadOrCreate<MapCatalog>(MapCatalogPath);
        ConfigureCatalog(catalog, new[] { prototypeOne, prototypeTwo, prototypeThree, mainMap });
        GameAssetRegistry registry = LoadOrCreate<GameAssetRegistry>(RegistryPath);
        ConfigureRegistry(registry, new[] { prototypeOne, prototypeTwo, prototypeThree, mainMap });

        CreateMainMenuSceneV2(catalog);
        UpdateMainScene(catalog, registry);
        EnsurePrototypeScenes();
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/RTS/Validate Game Menu Setup")]
    public static void ValidateGeneratedContent()
    {
        List<string> issues = new();

        ValidateMainMenuScene(issues);
        ValidatePrototypeScene(PrototypeMapOneScenePath, issues);
        ValidatePrototypeScene(PrototypeMapTwoScenePath, issues);
        ValidatePrototypeScene(PrototypeMapThreeScenePath, issues);

        if (issues.Count > 0)
            throw new System.InvalidOperationException("Game menu setup validation failed:\n" + string.Join("\n", issues));

        Debug.Log("Game menu setup validation passed.");
    }

    private static void ConfigureMainMap(MapDefinition map)
    {
        ConfigureMap(
            map,
            "main_scene",
            "Main Battlefield",
            MainScenePath,
            4,
            new[] { SkirmishTeamMode.OneVsOne, SkirmishTeamMode.TwoVsTwo });
    }

    private static void ConfigureMap(
        MapDefinition map,
        string mapId,
        string displayName,
        string scenePath,
        int maxPlayers,
        IReadOnlyList<SkirmishTeamMode> supportedModes)
    {
        SerializedObject serialized = new(map);
        SetString(serialized, "_mapId", mapId);
        SetString(serialized, "_displayName", displayName);
        SetString(serialized, "_scenePath", scenePath);
        SetInt(serialized, "_maxPlayers", maxPlayers);
        SetInt(serialized, "_defaultStartingResources", 500);
        SetSupportedModes(serialized.FindProperty("_supportedModes"), supportedModes);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
    }

    private static void ConfigureCatalog(MapCatalog catalog, IReadOnlyList<MapDefinition> mapsToRegister)
    {
        SerializedObject serialized = new(catalog);
        SerializedProperty maps = serialized.FindProperty("_maps");
        maps.arraySize = mapsToRegister.Count;
        for (int i = 0; i < mapsToRegister.Count; i++)
            maps.GetArrayElementAtIndex(i).objectReferenceValue = mapsToRegister[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void ConfigureRegistry(GameAssetRegistry registry, IReadOnlyList<MapDefinition> maps)
    {
        SerializedObject serialized = new(registry);
        SetRegistryArray(serialized.FindProperty("_maps"), new[]
        {
            ("prototype_map_1", (Object)maps[0]),
            ("prototype_map_2", (Object)maps[1]),
            ("prototype_map_3", (Object)maps[2]),
            ("main_scene", (Object)maps[3])
        });
        SetRegistryArray(serialized.FindProperty("_buildings"), new[]
        {
            ("military_base", (Object)AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Balance/MilitaryBase.asset")),
            ("heavy_factory", (Object)AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Balance/HeavyFactory.asset"))
        });
        SetRegistryArray(serialized.FindProperty("_units"), new[]
        {
            ("quad_autocannon", (Object)AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Balance/LightTank.asset")),
            ("medium_tank", (Object)AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Balance/MeadleTank.asset")),
            ("artillery", (Object)AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Balance/SelfPropelledArtillery.asset"))
        });
        SetRegistryArray(serialized.FindProperty("_productionItems"), new[]
        {
            ("quad_autocannon_production", (Object)AssetDatabase.LoadAssetAtPath<ProductionItemData>("Assets/Balance/LightTankProduction.asset")),
            ("medium_tank_production", (Object)AssetDatabase.LoadAssetAtPath<ProductionItemData>("Assets/Balance/MeadleTankProduction.asset")),
            ("artillery_production", (Object)AssetDatabase.LoadAssetAtPath<ProductionItemData>("Assets/Balance/SelfPropelledArtilleryProduction.asset"))
        });
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
    }

    private static void CreateMainMenuScene(MapCatalog catalog)
    {
        LoadMenuStyleAssets();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject eventSystem = new("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.position = Vector3.zero;

        Camera camera = CreateMenuCamera();
        Canvas canvas = CreateCanvas("MainMenuCanvas");
        canvas.worldCamera = camera;

        GameObject root = CreatePanel(canvas.transform, "Root", new Color(0.15f, 0.26f, 0.36f, 1f));
        GameMenuController controller = root.AddComponent<GameMenuController>();
        CreateBackground(root.transform);

        TMP_Text title = CreateText(root.transform, "Title", "STRATEGY", 56f, TextAlignmentOptions.Left);
        if (_menuFont != null)
            title.font = _menuFont;
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.94f, 0.98f, 1f, 1f);
        SetAnchors(title.rectTransform, 0.06f, 0.84f, 0.52f, 0.94f);

        TMP_Text subtitle = CreateText(root.transform, "Subtitle", "Тактичний запуск матчу", 20f, TextAlignmentOptions.Left);
        subtitle.color = new Color(0.68f, 0.92f, 1f, 0.95f);
        SetAnchors(subtitle.rectTransform, 0.06f, 0.805f, 0.52f, 0.85f);

        GameObject status = CreateText(root.transform, "Status", string.Empty, 18f, TextAlignmentOptions.Center).gameObject;
        status.GetComponent<TMP_Text>().color = new Color(0.95f, 0.99f, 1f, 0.95f);
        SetAnchors((RectTransform)status.transform, 0.18f, 0.025f, 0.82f, 0.075f);

        GameObject mainPanel = CreateAnchoredContainer(root.transform, "MainPanel", 0.06f, 0.14f, 0.94f, 0.76f);
        GameObject mainNav = CreateWindowPanel(mainPanel.transform, "MainNavigation", 0.02f, 0.05f, 0.34f, 0.96f);
        AddVerticalLayout(mainNav, 26, 24, 18);
        CreateSectionHeader(mainNav.transform, "ГОЛОВНЕ МЕНЮ");
        Button skirmish = CreateButton(mainNav.transform, "SkirmishButton", "СХВАТКА");
        SetPreferredHeight(skirmish.gameObject, 58f);
        Button load = CreateButton(mainNav.transform, "LoadButton", "ЗАВАНТАЖЕННЯ");
        Button settings = CreateButton(mainNav.transform, "SettingsButton", "НАЛАШТУВАННЯ");
        Button exit = CreateButton(mainNav.transform, "ExitButton", "ВИХІД");

        TMP_Text mainHint = CreateText(mainNav.transform, "MainHint", "Натисни СХВАТКА, щоб обрати карту, слот старту, ресурси та ботів.", 16f, TextAlignmentOptions.Left);
        mainHint.color = new Color(0.78f, 0.9f, 1f, 0.86f);
        SetPreferredHeight(mainHint.gameObject, 70f);

        GameObject mainBriefing = CreateWindowPanel(mainPanel.transform, "MainBriefing", 0.38f, 0.05f, 0.98f, 0.96f);
        AddVerticalLayout(mainBriefing, 30, 30, 16);
        CreateSectionHeader(mainBriefing.transform, "КОМАНДНИЙ ЦЕНТР");
        TMP_Text briefingTitle = CreateText(mainBriefing.transform, "BriefingTitle", "Швидкий старт стратегії", 32f, TextAlignmentOptions.Left);
        briefingTitle.fontStyle = FontStyles.Bold;
        SetPreferredHeight(briefingTitle.gameObject, 48f);
        TMP_Text briefing = CreateText(mainBriefing.transform, "BriefingText",
            "1. Відкрий СХВАТКА.\n2. Обери режим: з ботами або мережевий.\n3. Вибери карту, складність AI, ресурси та spawn.\n4. Натисни ЗАПУСК.",
            22f,
            TextAlignmentOptions.Left);
        briefing.color = new Color(0.88f, 0.96f, 1f, 0.95f);
        SetPreferredHeight(briefing.gameObject, 175f);
        TMP_Text currentContent = CreateText(mainBriefing.transform, "CurrentContent",
            "Доступна карта: Main Battlefield\nПідтримка: 1 vs 1, 2 vs 2\nOffline: боти Easy / Medium / Hard",
            19f,
            TextAlignmentOptions.Left);
        currentContent.color = new Color(0.66f, 0.94f, 1f, 0.96f);
        SetPreferredHeight(currentContent.gameObject, 96f);

        GameObject modePanel = CreateAnchoredContainer(root.transform, "SkirmishModePanel", 0.2f, 0.2f, 0.8f, 0.72f);
        GameObject modeWindow = CreateWindowPanel(modePanel.transform, "ModeWindow", 0f, 0f, 1f, 1f);
        AddVerticalLayout(modeWindow, 30, 30, 18);
        CreateSectionHeader(modeWindow.transform, "СХВАТКА");
        TMP_Text modeHint = CreateText(modeWindow.transform, "ModeHint", "Обери тип матчу. Для швидкого тесту гри запускай режим з ботами.", 18f, TextAlignmentOptions.Left);
        modeHint.color = new Color(0.86f, 0.96f, 1f, 0.9f);
        SetPreferredHeight(modeHint.gameObject, 52f);
        Button offline = CreateButton(modeWindow.transform, "OfflineBotsButton", "З БОТАМИ");
        SetPreferredHeight(offline.gameObject, 62f);
        Button online = CreateButton(modeWindow.transform, "OnlineButton", "МЕРЕЖЕВИЙ РЕЖИМ");
        SetPreferredHeight(online.gameObject, 62f);
        Button modeBack = CreateButton(modeWindow.transform, "ModeBackButton", "НАЗАД");

        GameObject setupPanel = CreateAnchoredContainer(root.transform, "SkirmishPanel", 0.04f, 0.055f, 0.96f, 0.86f);
        GameObject setupWindow = CreateWindowPanel(setupPanel.transform, "SkirmishWindow", 0f, 0f, 1f, 1f);
        TMP_Text setupTitle = CreateText(setupWindow.transform, "SkirmishTitle", "СХВАТКА: НАЛАШТУВАННЯ МАТЧУ", 30f, TextAlignmentOptions.Left);
        setupTitle.fontStyle = FontStyles.Bold;
        SetAnchors(setupTitle.rectTransform, 0.025f, 0.91f, 0.72f, 0.985f);

        GameObject playersSection = CreateWindowPanel(setupWindow.transform, "PlayersSection", 0.025f, 0.18f, 0.38f, 0.885f);
        AddVerticalLayout(playersSection, 20, 20, 12);
        CreateSectionHeader(playersSection.transform, "ГРАВЦІ ТА СЛОТИ");
        TMP_Dropdown spawnDropdown = CreateLabeledDropdown(playersSection.transform, "LocalSpawnDropdown", "Місце старту гравця");
        TMP_Text summary = CreateText(playersSection.transform, "SlotSummary", string.Empty, 18f, TextAlignmentOptions.Left);
        summary.color = new Color(0.88f, 0.96f, 1f, 0.95f);
        SetFlexibleHeight(summary.gameObject, 1f, 260f);

        GameObject mapSection = CreateWindowPanel(setupWindow.transform, "MapSection", 0.405f, 0.55f, 0.975f, 0.885f);
        AddVerticalLayout(mapSection, 20, 20, 8);
        CreateSectionHeader(mapSection.transform, "КАРТА");
        Image mapPreview = CreatePreviewImage(mapSection.transform, "MapPreview", _backgroundSprite);
        TMP_Text mapPreviewTitle = CreateText(mapSection.transform, "MapPreviewTitle", "Main Battlefield", 20f, TextAlignmentOptions.Left);
        mapPreviewTitle.fontStyle = FontStyles.Bold;
        TMP_Text mapPreviewDetails = CreateText(mapSection.transform, "MapPreviewDetails", string.Empty, 15f, TextAlignmentOptions.Left);
        mapPreviewDetails.color = new Color(0.82f, 0.93f, 1f, 0.9f);
        SetPreferredHeight(mapPreviewDetails.gameObject, 36f);
        TMP_Dropdown mapDropdown = CreateLabeledDropdown(mapSection.transform, "MapDropdown", "Обрати карту");

        GameObject optionsSection = CreateWindowPanel(setupWindow.transform, "OptionsSection", 0.405f, 0.18f, 0.975f, 0.525f);
        AddVerticalLayout(optionsSection, 20, 20, 8);
        CreateSectionHeader(optionsSection.transform, "ПАРАМЕТРИ");
        GameObject optionsGrid = CreateLayoutContainer(optionsSection.transform, "OptionsGrid");
        SetFlexibleHeight(optionsGrid, 1f, 168f);
        AddHorizontalLayout(optionsGrid, 0, 0, 18);
        GameObject leftOptions = CreateLayoutContainer(optionsGrid.transform, "LeftOptions");
        AddVerticalLayout(leftOptions, 0, 0, 8);
        SetFlexibleWidth(leftOptions, 1f);
        GameObject rightOptions = CreateLayoutContainer(optionsGrid.transform, "RightOptions");
        AddVerticalLayout(rightOptions, 0, 0, 8);
        SetFlexibleWidth(rightOptions, 1f);
        TMP_Dropdown modeDropdown = CreateLabeledDropdown(leftOptions.transform, "TeamModeDropdown", "Режим команд");
        TMP_Dropdown difficultyDropdown = CreateLabeledDropdown(leftOptions.transform, "DifficultyDropdown", "Складність ботів");
        TMP_Dropdown resourcesDropdown = CreateLabeledDropdown(rightOptions.transform, "ResourcesDropdown", "Стартові ресурси");
        TMP_InputField resourcesInput = CreateLabeledInput(rightOptions.transform, "CustomResourcesInput", "Свій ресурс", "500");

        GameObject actionBar = CreateAnchoredContainer(setupWindow.transform, "ActionBar", 0.025f, 0.035f, 0.975f, 0.145f);
        AddHorizontalLayout(actionBar, 0, 0, 18);
        Button backSetup = CreateButton(actionBar.transform, "BackFromSkirmishButton", "НАЗАД");
        SetPreferredSize(backSetup.gameObject, 230f, 52f);
        CreateFlexibleSpace(actionBar.transform, "ActionSpacer");
        Button start = CreateButton(actionBar.transform, "StartOfflineButton", "ЗАПУСК");
        SetPreferredSize(start.gameObject, 270f, 52f);
        SetButtonColor(start, new Color(0.05f, 0.48f, 0.95f, 1f));
        Button host = CreateButton(actionBar.transform, "HostOnlineButton", "СТВОРИТИ LOBBY");
        SetPreferredSize(host.gameObject, 270f, 52f);

        GameObject onlinePanel = CreateAnchoredContainer(root.transform, "OnlinePanel", 0.28f, 0.22f, 0.72f, 0.70f);
        GameObject onlineWindow = CreateWindowPanel(onlinePanel.transform, "OnlineWindow", 0f, 0f, 1f, 1f);
        AddVerticalLayout(onlineWindow, 30, 30, 16);
        CreateSectionHeader(onlineWindow.transform, "МЕРЕЖЕВИЙ РЕЖИМ");
        TMP_Text onlineHint = CreateText(onlineWindow.transform, "OnlineHint", "Підключись за кодом lobby або налаштуй host-матч із тими самими правилами, що й схватка з ботами.", 17f, TextAlignmentOptions.Left);
        onlineHint.color = new Color(0.86f, 0.96f, 1f, 0.9f);
        SetPreferredHeight(onlineHint.gameObject, 74f);
        TMP_InputField joinInput = CreateInput(onlineWindow.transform, "JoinCodeInput", "Код lobby");
        Button openHost = CreateButton(onlineWindow.transform, "OpenOnlineHostSetupButton", "НАЛАШТУВАТИ HOST");
        Button join = CreateButton(onlineWindow.transform, "JoinOnlineButton", "ПІДКЛЮЧИТИСЬ");
        Button backOnline = CreateButton(onlineWindow.transform, "BackFromOnlineButton", "НАЗАД");

        GameObject loadPanel = CreateAnchoredContainer(root.transform, "LoadPanel", 0.32f, 0.26f, 0.68f, 0.66f);
        GameObject loadWindow = CreateWindowPanel(loadPanel.transform, "LoadWindow", 0f, 0f, 1f, 1f);
        AddVerticalLayout(loadWindow, 28, 28, 16);
        CreateSectionHeader(loadWindow.transform, "ЗАВАНТАЖЕННЯ");
        TMP_Text loadHint = CreateText(loadWindow.transform, "LoadHint", "Збереження доступні для offline-матчів із ботами.", 16f, TextAlignmentOptions.Left);
        loadHint.color = new Color(0.86f, 0.96f, 1f, 0.9f);
        TMP_Dropdown saveDropdown = CreateLabeledDropdown(loadWindow.transform, "SaveDropdown", "Файл збереження");
        Button loadSave = CreateButton(loadWindow.transform, "LoadSaveButton", "ЗАВАНТАЖИТИ");
        Button backLoad = CreateButton(loadWindow.transform, "BackFromLoadButton", "НАЗАД");

        GameObject settingsPanel = CreateAnchoredContainer(root.transform, "SettingsPanel", 0.32f, 0.26f, 0.68f, 0.66f);
        GameObject settingsWindow = CreateWindowPanel(settingsPanel.transform, "SettingsWindow", 0f, 0f, 1f, 1f);
        AddVerticalLayout(settingsWindow, 28, 28, 16);
        CreateSectionHeader(settingsWindow.transform, "НАЛАШТУВАННЯ");
        TMP_Dropdown resolution = CreateLabeledDropdown(settingsWindow.transform, "ResolutionDropdown", "Роздільна здатність");
        Button applyResolution = CreateButton(settingsWindow.transform, "ApplyResolutionButton", "ЗАСТОСУВАТИ");
        Button backSettings = CreateButton(settingsWindow.transform, "BackFromSettingsButton", "НАЗАД");

        SerializedObject serialized = new(controller);
        SetObject(serialized, "_mapCatalog", catalog);
        SetObject(serialized, "_mainPanel", mainPanel);
        SetObject(serialized, "_skirmishModePanel", modePanel);
        SetObject(serialized, "_skirmishPanel", setupPanel);
        SetObject(serialized, "_onlinePanel", onlinePanel);
        SetObject(serialized, "_loadPanel", loadPanel);
        SetObject(serialized, "_settingsPanel", settingsPanel);
        SetObject(serialized, "_skirmishButton", skirmish);
        SetObject(serialized, "_loadButton", load);
        SetObject(serialized, "_settingsButton", settings);
        SetObject(serialized, "_exitButton", exit);
        SetObject(serialized, "_offlineBotsButton", offline);
        SetObject(serialized, "_onlineButton", online);
        SetObject(serialized, "_openOnlineHostSetupButton", openHost);
        SetObject(serialized, "_backFromModeButton", modeBack);
        SetObject(serialized, "_mapDropdown", mapDropdown);
        SetObject(serialized, "_teamModeDropdown", modeDropdown);
        SetObject(serialized, "_difficultyDropdown", difficultyDropdown);
        SetObject(serialized, "_resourcesDropdown", resourcesDropdown);
        SetObject(serialized, "_customResourcesInput", resourcesInput);
        SetObject(serialized, "_localSpawnDropdown", spawnDropdown);
        SetObject(serialized, "_slotSummaryText", summary);
        SetObject(serialized, "_startOfflineButton", start);
        SetObject(serialized, "_hostOnlineButton", host);
        SetObject(serialized, "_backFromSkirmishButton", backSetup);
        SetObject(serialized, "_mapPreviewImage", mapPreview);
        SetObject(serialized, "_fallbackMapPreview", _backgroundSprite);
        SetObject(serialized, "_mapPreviewTitleText", mapPreviewTitle);
        SetObject(serialized, "_mapPreviewDetailsText", mapPreviewDetails);
        SetObject(serialized, "_joinCodeInput", joinInput);
        SetObject(serialized, "_joinOnlineButton", join);
        SetObject(serialized, "_backFromOnlineButton", backOnline);
        SetObject(serialized, "_saveDropdown", saveDropdown);
        SetObject(serialized, "_loadSaveButton", loadSave);
        SetObject(serialized, "_backFromLoadButton", backLoad);
        SetObject(serialized, "_resolutionDropdown", resolution);
        SetObject(serialized, "_applyResolutionButton", applyResolution);
        SetObject(serialized, "_backFromSettingsButton", backSettings);
        SetObject(serialized, "_statusText", status.GetComponent<TMP_Text>());
        SetEnumArray(serialized, "_availableTeamModes", new[]
        {
            (int)SkirmishTeamMode.OneVsOne,
            (int)SkirmishTeamMode.TwoVsTwo
        });
        SetEnumArray(serialized, "_availableDifficulties", new[]
        {
            (int)AiDifficultyLevel.Easy,
            (int)AiDifficultyLevel.Medium,
            (int)AiDifficultyLevel.Hard
        });
        SetInt(serialized, "_defaultDifficultyIndex", 1);
        SetIntArray(serialized, "_startingResourcePresets", new[] { 500, 1000, 2000, 5000 });
        SetBool(serialized, "_allowCustomStartingResources", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        mainPanel.SetActive(true);
        modePanel.SetActive(false);
        setupPanel.SetActive(false);
        onlinePanel.SetActive(false);
        loadPanel.SetActive(false);
        settingsPanel.SetActive(false);
        RebuildMenuLayout(root.transform);

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
    }

    private static void CreateMainMenuSceneV2(MapCatalog catalog)
    {
        LoadMenuStyleAssets();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject eventSystem = new("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.position = Vector3.zero;

        Camera camera = CreateMenuCamera();
        Canvas canvas = CreateCanvas("MainMenuCanvas");
        canvas.worldCamera = camera;

        GameObject root = CreatePanel(canvas.transform, "Root", new Color(0.15f, 0.26f, 0.36f, 1f));
        GameMenuController controller = root.AddComponent<GameMenuController>();
        CreateBackground(root.transform);

        TMP_Text title = CreateText(root.transform, "Title", "STRATEGY", 54f, TextAlignmentOptions.Left);
        if (_menuFont != null)
            title.font = _menuFont;
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.95f, 0.99f, 1f, 1f);
        SetAnchors(title.rectTransform, 0.055f, 0.855f, 0.36f, 0.94f);

        TMP_Text subtitle = CreateText(root.transform, "Subtitle", "Тактичний запуск матчу", 18f, TextAlignmentOptions.Left);
        subtitle.color = new Color(0.70f, 0.92f, 1f, 0.94f);
        SetAnchors(subtitle.rectTransform, 0.06f, 0.815f, 0.42f, 0.855f);

        GameObject status = CreateText(root.transform, "Status", string.Empty, 18f, TextAlignmentOptions.Center).gameObject;
        status.GetComponent<TMP_Text>().color = new Color(0.95f, 0.99f, 1f, 0.95f);
        SetAnchors((RectTransform)status.transform, 0.18f, 0.025f, 0.82f, 0.075f);

        GameObject mainPanel = CreateAnchoredContainer(root.transform, "MainPanel", 0.075f, 0.10f, 0.925f, 0.78f);
        GameObject mainNav = CreateWindowPanel(mainPanel.transform, "MainNavigation", 0.02f, 0.04f, 0.36f, 0.96f);
        AddVerticalLayout(mainNav, 28, 28, 14);
        CreateSectionHeader(mainNav.transform, "ГОЛОВНЕ МЕНЮ");
        Button skirmish = CreateButton(mainNav.transform, "SkirmishButton", "СХВАТКА");
        Button load = CreateButton(mainNav.transform, "LoadButton", "ЗАВАНТАЖЕННЯ");
        Button settings = CreateButton(mainNav.transform, "SettingsButton", "НАЛАШТУВАННЯ");
        Button exit = CreateButton(mainNav.transform, "ExitButton", "ВИХІД");
        SetPreferredHeight(skirmish.gameObject, 54f);
        SetPreferredHeight(load.gameObject, 54f);
        SetPreferredHeight(settings.gameObject, 54f);
        SetPreferredHeight(exit.gameObject, 54f);
        TMP_Text mainHint = CreateText(mainNav.transform, "MainHint", "Натисни СХВАТКА, щоб обрати карту, команди, ресурси та ботів.", 15f, TextAlignmentOptions.Left);
        mainHint.color = new Color(0.78f, 0.9f, 1f, 0.86f);
        SetPreferredHeight(mainHint.gameObject, 60f);

        GameObject mainBriefing = CreateWindowPanel(mainPanel.transform, "MainBriefing", 0.42f, 0.04f, 0.98f, 0.96f);
        AddVerticalLayout(mainBriefing, 30, 28, 14);
        CreateSectionHeader(mainBriefing.transform, "КОМАНДНИЙ ЦЕНТР");
        TMP_Text briefingTitle = CreateText(mainBriefing.transform, "BriefingTitle", "Швидкий старт стратегії", 30f, TextAlignmentOptions.Left);
        briefingTitle.fontStyle = FontStyles.Bold;
        SetPreferredHeight(briefingTitle.gameObject, 44f);
        TMP_Text briefing = CreateText(mainBriefing.transform, "BriefingText",
            "1. Відкрий СХВАТКА.\n2. Обери режим з ботами або мережевий.\n3. Вибери карту, slot, команду, складність AI і стартові ресурси.\n4. Натисни ЗАПУСК.",
            20f,
            TextAlignmentOptions.Left);
        briefing.color = new Color(0.88f, 0.96f, 1f, 0.95f);
        SetPreferredHeight(briefing.gameObject, 150f);
        TMP_Text currentContent = CreateText(mainBriefing.transform, "CurrentContent",
            "Доступні карти: Prototype 1/2/3, Main Battlefield\nРежими: 1 vs 1, 3 гравці, 2 vs 2\nAI: Easy / Medium / Hard по кожному слоту",
            17f,
            TextAlignmentOptions.Left);
        currentContent.color = new Color(0.70f, 0.94f, 1f, 0.96f);
        SetPreferredHeight(currentContent.gameObject, 90f);

        GameObject modePanel = CreateAnchoredContainer(root.transform, "SkirmishModePanel", 0.16f, 0.16f, 0.84f, 0.76f);
        GameObject modeWindow = CreateWindowPanel(modePanel.transform, "ModeWindow", 0f, 0f, 1f, 1f);
        AddVerticalLayout(modeWindow, 42, 40, 18);
        CreateSectionHeader(modeWindow.transform, "СХВАТКА");
        TMP_Text modeHint = CreateText(modeWindow.transform, "ModeHint", "Обери тип матчу. Для швидкого тесту запускай режим з ботами.", 17f, TextAlignmentOptions.Left);
        modeHint.color = new Color(0.86f, 0.96f, 1f, 0.9f);
        SetPreferredHeight(modeHint.gameObject, 42f);
        Button offline = CreateButton(modeWindow.transform, "OfflineBotsButton", "З БОТАМИ");
        Button online = CreateButton(modeWindow.transform, "OnlineButton", "МЕРЕЖЕВИЙ РЕЖИМ");
        Button modeBack = CreateButton(modeWindow.transform, "ModeBackButton", "НАЗАД");
        SetPreferredHeight(offline.gameObject, 56f);
        SetPreferredHeight(online.gameObject, 56f);
        SetPreferredHeight(modeBack.gameObject, 52f);

        GameObject setupPanel = CreateAnchoredContainer(root.transform, "SkirmishPanel", 0.065f, 0.12f, 0.935f, 0.80f);
        GameObject setupWindow = CreateWindowPanel(setupPanel.transform, "SkirmishWindow", 0f, 0f, 1f, 1f);
        TMP_Text setupTitle = CreateText(setupWindow.transform, "SkirmishTitle", "СХВАТКА: НАЛАШТУВАННЯ МАТЧУ", 22f, TextAlignmentOptions.Left);
        setupTitle.fontStyle = FontStyles.Bold;
        SetAnchors(setupTitle.rectTransform, 0.025f, 0.91f, 0.58f, 0.965f);

        List<Button> mapButtons = new();
        List<TMP_Text> mapButtonLabels = new();
        List<TMP_Text> slotSpawnLabels = new();
        List<TMP_Dropdown> slotControllerDropdowns = new();
        List<TMP_Dropdown> slotTeamDropdowns = new();
        List<TMP_Dropdown> slotDifficultyDropdowns = new();

        GameObject playersSection = CreateWindowPanel(setupWindow.transform, "PlayersSection", 0.025f, 0.21f, 0.505f, 0.88f);
        CreateAnchoredText(playersSection.transform, "PlayersHeader", "ГРАВЦІ ТА СЛОТИ", 15f, TextAlignmentOptions.Left, 0.035f, 0.88f, 0.96f, 0.965f);
        CreateSlotHeader(playersSection.transform);
        for (int i = 0; i < 4; i++)
            CreateSlotRowFixed(playersSection.transform, i, slotSpawnLabels, slotControllerDropdowns, slotTeamDropdowns, slotDifficultyDropdowns);

        GameObject mapSection = CreateWindowPanel(setupWindow.transform, "MapSection", 0.535f, 0.64f, 0.975f, 0.88f);
        CreateAnchoredText(mapSection.transform, "MapHeader", "КАРТА", 15f, TextAlignmentOptions.Left, 0.035f, 0.82f, 0.96f, 0.955f);
        Image mapPreview = CreateAnchoredPreviewImage(mapSection.transform, "MapPreview", _backgroundSprite, 0.035f, 0.38f, 0.965f, 0.78f);
        TMP_Text mapPreviewTitle = CreateAnchoredText(mapSection.transform, "MapPreviewTitle", "Prototype Map", 14f, TextAlignmentOptions.Left, 0.035f, 0.20f, 0.965f, 0.35f);
        mapPreviewTitle.fontStyle = FontStyles.Bold;
        TMP_Text mapPreviewDetails = CreateAnchoredText(mapSection.transform, "MapPreviewDetails", string.Empty, 11f, TextAlignmentOptions.Left, 0.035f, 0.035f, 0.965f, 0.18f);
        mapPreviewDetails.color = new Color(0.82f, 0.93f, 1f, 0.9f);

        GameObject mapListSection = CreateWindowPanel(setupWindow.transform, "MapListSection", 0.535f, 0.37f, 0.975f, 0.62f);
        CreateAnchoredText(mapListSection.transform, "MapListHeader", "ОБЕРИ КАРТУ", 14f, TextAlignmentOptions.Left, 0.035f, 0.84f, 0.96f, 0.96f);
        TMP_Dropdown mapDropdown = CreateAnchoredDropdown(mapListSection.transform, "MapDropdown", 0.035f, 0.67f, 0.965f, 0.80f);
        for (int i = 0; i < 4; i++)
            CreateMapRowButtonFixed(mapListSection.transform, i, mapButtons, mapButtonLabels);

        GameObject optionsSection = CreateWindowPanel(setupWindow.transform, "OptionsSection", 0.535f, 0.17f, 0.975f, 0.35f);
        CreateAnchoredText(optionsSection.transform, "OptionsHeader", "ПАРАМЕТРИ", 14f, TextAlignmentOptions.Left, 0.035f, 0.78f, 0.96f, 0.95f);
        CreateAnchoredText(optionsSection.transform, "ResourcesLabel", "Стартові ресурси", 11f, TextAlignmentOptions.Left, 0.035f, 0.50f, 0.46f, 0.68f);
        TMP_Dropdown resourcesDropdown = CreateAnchoredDropdown(optionsSection.transform, "ResourcesDropdown", 0.035f, 0.18f, 0.46f, 0.46f);
        CreateAnchoredText(optionsSection.transform, "CustomResourcesInputLabel", "Свій ресурс", 11f, TextAlignmentOptions.Left, 0.53f, 0.50f, 0.965f, 0.68f);
        TMP_InputField resourcesInput = CreateAnchoredInput(optionsSection.transform, "CustomResourcesInput", "500", 0.53f, 0.18f, 0.965f, 0.46f);

        GameObject actionBar = CreateAnchoredContainer(setupWindow.transform, "ActionBar", 0.025f, 0.055f, 0.975f, 0.135f);
        Button backSetup = CreateAnchoredButton(actionBar.transform, "BackFromSkirmishButton", "НАЗАД", 0f, 0.18f, 0.14f, 0.82f);
        Button start = CreateAnchoredButton(actionBar.transform, "StartOfflineButton", "ЗАПУСК", 0.83f, 0.18f, 1f, 0.82f);
        SetButtonColor(start, new Color(0.05f, 0.48f, 0.95f, 1f));
        Button host = CreateAnchoredButton(actionBar.transform, "HostOnlineButton", "СТВОРИТИ LOBBY", 0.62f, 0.18f, 0.82f, 0.82f);

        GameObject onlinePanel = CreateAnchoredContainer(root.transform, "OnlinePanel", 0.30f, 0.27f, 0.70f, 0.73f);
        GameObject onlineWindow = CreateWindowPanel(onlinePanel.transform, "OnlineWindow", 0f, 0f, 1f, 1f);
        CreateAnchoredText(onlineWindow.transform, "OnlineHeader", "МЕРЕЖЕВИЙ РЕЖИМ", 16f, TextAlignmentOptions.Left, 0.055f, 0.82f, 0.94f, 0.94f);
        TMP_Text onlineHint = CreateAnchoredText(onlineWindow.transform, "OnlineHint", "Підключись за кодом lobby або налаштуй host-матч з картою, слотами, ботами та ресурсами.", 14f, TextAlignmentOptions.Left, 0.055f, 0.64f, 0.94f, 0.79f);
        onlineHint.color = new Color(0.86f, 0.96f, 1f, 0.9f);
        TMP_InputField joinInput = CreateAnchoredInput(onlineWindow.transform, "JoinCodeInput", "Код lobby", 0.055f, 0.52f, 0.94f, 0.61f);
        Button openHost = CreateAnchoredButton(onlineWindow.transform, "OpenOnlineHostSetupButton", "НАЛАШТУВАТИ HOST", 0.055f, 0.37f, 0.94f, 0.47f);
        Button join = CreateAnchoredButton(onlineWindow.transform, "JoinOnlineButton", "ПІДКЛЮЧИТИСЬ", 0.055f, 0.23f, 0.94f, 0.33f);
        Button backOnline = CreateAnchoredButton(onlineWindow.transform, "BackFromOnlineButton", "НАЗАД", 0.055f, 0.08f, 0.94f, 0.18f);

        GameObject loadPanel = CreateAnchoredContainer(root.transform, "LoadPanel", 0.32f, 0.26f, 0.68f, 0.66f);
        GameObject loadWindow = CreateWindowPanel(loadPanel.transform, "LoadWindow", 0f, 0f, 1f, 1f);
        AddVerticalLayout(loadWindow, 28, 28, 16);
        CreateSectionHeader(loadWindow.transform, "ЗАВАНТАЖЕННЯ");
        TMP_Text loadHint = CreateText(loadWindow.transform, "LoadHint", "Збереження доступні для offline-матчів із ботами.", 16f, TextAlignmentOptions.Left);
        loadHint.color = new Color(0.86f, 0.96f, 1f, 0.9f);
        TMP_Dropdown saveDropdown = CreateLabeledDropdown(loadWindow.transform, "SaveDropdown", "Файл збереження");
        Button loadSave = CreateButton(loadWindow.transform, "LoadSaveButton", "ЗАВАНТАЖИТИ");
        Button backLoad = CreateButton(loadWindow.transform, "BackFromLoadButton", "НАЗАД");

        GameObject settingsPanel = CreateAnchoredContainer(root.transform, "SettingsPanel", 0.32f, 0.26f, 0.68f, 0.66f);
        GameObject settingsWindow = CreateWindowPanel(settingsPanel.transform, "SettingsWindow", 0f, 0f, 1f, 1f);
        AddVerticalLayout(settingsWindow, 28, 28, 16);
        CreateSectionHeader(settingsWindow.transform, "НАЛАШТУВАННЯ");
        TMP_Dropdown resolution = CreateLabeledDropdown(settingsWindow.transform, "ResolutionDropdown", "Роздільна здатність");
        Button applyResolution = CreateButton(settingsWindow.transform, "ApplyResolutionButton", "ЗАСТОСУВАТИ");
        Button backSettings = CreateButton(settingsWindow.transform, "BackFromSettingsButton", "НАЗАД");

        SerializedObject serialized = new(controller);
        SetObject(serialized, "_mapCatalog", catalog);
        SetObject(serialized, "_mainPanel", mainPanel);
        SetObject(serialized, "_skirmishModePanel", modePanel);
        SetObject(serialized, "_skirmishPanel", setupPanel);
        SetObject(serialized, "_onlinePanel", onlinePanel);
        SetObject(serialized, "_loadPanel", loadPanel);
        SetObject(serialized, "_settingsPanel", settingsPanel);
        SetObject(serialized, "_skirmishButton", skirmish);
        SetObject(serialized, "_loadButton", load);
        SetObject(serialized, "_settingsButton", settings);
        SetObject(serialized, "_exitButton", exit);
        SetObject(serialized, "_offlineBotsButton", offline);
        SetObject(serialized, "_onlineButton", online);
        SetObject(serialized, "_openOnlineHostSetupButton", openHost);
        SetObject(serialized, "_backFromModeButton", modeBack);
        SetObject(serialized, "_mapDropdown", mapDropdown);
        SetObject(serialized, "_teamModeDropdown", null);
        SetObject(serialized, "_resourcesDropdown", resourcesDropdown);
        SetObject(serialized, "_customResourcesInput", resourcesInput);
        SetObject(serialized, "_startOfflineButton", start);
        SetObject(serialized, "_hostOnlineButton", host);
        SetObject(serialized, "_backFromSkirmishButton", backSetup);
        SetObjectArray(serialized.FindProperty("_mapButtons"), mapButtons);
        SetObjectArray(serialized.FindProperty("_mapButtonLabels"), mapButtonLabels);
        SetObjectArray(serialized.FindProperty("_slotSpawnLabels"), slotSpawnLabels);
        SetObjectArray(serialized.FindProperty("_slotControllerDropdowns"), slotControllerDropdowns);
        SetObjectArray(serialized.FindProperty("_slotTeamDropdowns"), slotTeamDropdowns);
        SetObjectArray(serialized.FindProperty("_slotDifficultyDropdowns"), slotDifficultyDropdowns);
        SetObject(serialized, "_mapPreviewImage", mapPreview);
        SetObject(serialized, "_fallbackMapPreview", _backgroundSprite);
        SetObject(serialized, "_mapPreviewTitleText", mapPreviewTitle);
        SetObject(serialized, "_mapPreviewDetailsText", mapPreviewDetails);
        SetObject(serialized, "_joinCodeInput", joinInput);
        SetObject(serialized, "_joinOnlineButton", join);
        SetObject(serialized, "_backFromOnlineButton", backOnline);
        SetObject(serialized, "_saveDropdown", saveDropdown);
        SetObject(serialized, "_loadSaveButton", loadSave);
        SetObject(serialized, "_backFromLoadButton", backLoad);
        SetObject(serialized, "_resolutionDropdown", resolution);
        SetObject(serialized, "_applyResolutionButton", applyResolution);
        SetObject(serialized, "_backFromSettingsButton", backSettings);
        SetObject(serialized, "_statusText", status.GetComponent<TMP_Text>());
        SetEnumArray(serialized, "_availableTeamModes", new[]
        {
            (int)SkirmishTeamMode.OneVsOne,
            (int)SkirmishTeamMode.ThreePlayer,
            (int)SkirmishTeamMode.TwoVsTwo
        });
        SetEnumArray(serialized, "_availableDifficulties", new[]
        {
            (int)AiDifficultyLevel.Easy,
            (int)AiDifficultyLevel.Medium,
            (int)AiDifficultyLevel.Hard
        });
        SetInt(serialized, "_defaultDifficultyIndex", 1);
        SetIntArray(serialized, "_startingResourcePresets", new[] { 500, 1000, 2000, 5000 });
        SetBool(serialized, "_allowCustomStartingResources", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        mainPanel.SetActive(true);
        modePanel.SetActive(false);
        setupPanel.SetActive(false);
        onlinePanel.SetActive(false);
        loadPanel.SetActive(false);
        settingsPanel.SetActive(false);
        RebuildMenuLayout(root.transform);

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
    }

    private static void ValidateMainMenuScene(List<string> issues)
    {
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        TMP_InputField[] inputs = Object.FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < inputs.Length; i++)
        {
            TMP_InputField input = inputs[i];
            if (input.textViewport == null)
                issues.Add($"{MainMenuScenePath}: TMP_InputField '{input.name}' has no textViewport.");
        }

        GameMenuController controller = Object.FindFirstObjectByType<GameMenuController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            issues.Add($"{MainMenuScenePath}: GameMenuController is missing.");
        }
        else
        {
            SerializedObject serialized = new(controller);
            if (serialized.FindProperty("_mapDropdown")?.objectReferenceValue == null)
                issues.Add($"{MainMenuScenePath}: GameMenuController._mapDropdown is not assigned.");
        }

        GameObject mainPanel = FindSceneObject("MainPanel");
        GameObject modePanel = FindSceneObject("SkirmishModePanel");
        GameObject setupPanel = FindSceneObject("SkirmishPanel");
        GameObject onlinePanel = FindSceneObject("OnlinePanel");
        GameObject loadPanel = FindSceneObject("LoadPanel");
        GameObject settingsPanel = FindSceneObject("SettingsPanel");

        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (modePanel != null)
            modePanel.SetActive(false);
        if (setupPanel != null)
            setupPanel.SetActive(true);
        if (onlinePanel != null)
            onlinePanel.SetActive(false);
        if (loadPanel != null)
            loadPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Canvas.ForceUpdateCanvases();
        if (setupPanel != null)
            RebuildMenuLayout(setupPanel.transform);

        RectTransform window = FindSceneObject("SkirmishWindow")?.GetComponent<RectTransform>();
        if (window == null)
        {
            issues.Add($"{MainMenuScenePath}: SkirmishWindow is missing.");
            return;
        }

        ValidateChildInside(window, "PlayersSection", issues);
        ValidateChildInside(window, "MapSection", issues);
        ValidateChildInside(window, "MapListSection", issues);
        ValidateChildInside(window, "OptionsSection", issues);
        ValidateChildInside(window, "ActionBar", issues);
    }

    private static void ValidateChildInside(RectTransform window, string childName, List<string> issues)
    {
        GameObject child = FindSceneObject(childName);
        RectTransform childRect = child != null ? child.GetComponent<RectTransform>() : null;

        if (childRect == null)
        {
            issues.Add($"{MainMenuScenePath}: {childName} is missing.");
            return;
        }

        if (!IsRectInside(window, childRect, 1f))
            issues.Add($"{MainMenuScenePath}: {childName} is outside SkirmishWindow.");
    }

    private static bool IsRectInside(RectTransform parent, RectTransform child, float tolerance)
    {
        Vector3[] parentCorners = new Vector3[4];
        Vector3[] childCorners = new Vector3[4];
        parent.GetWorldCorners(parentCorners);
        child.GetWorldCorners(childCorners);

        float minX = Mathf.Min(parentCorners[0].x, parentCorners[2].x) - tolerance;
        float maxX = Mathf.Max(parentCorners[0].x, parentCorners[2].x) + tolerance;
        float minY = Mathf.Min(parentCorners[0].y, parentCorners[2].y) - tolerance;
        float maxY = Mathf.Max(parentCorners[0].y, parentCorners[2].y) + tolerance;

        for (int i = 0; i < childCorners.Length; i++)
        {
            Vector3 corner = childCorners[i];
            if (corner.x < minX || corner.x > maxX || corner.y < minY || corner.y > maxY)
                return false;
        }

        return true;
    }

    private static void ValidatePrototypeScene(string scenePath, List<string> issues)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        if (FindSceneObject("PrototypeMapVariantDecor") != null)
            issues.Add($"{scenePath}: PrototypeMapVariantDecor should not exist.");

        ValidateNoPrototypeCleanupTargets<BuildingProduction>(scenePath, issues);
        ValidateNoPrototypeCleanupTargets<BuildingHealth>(scenePath, issues);
        ValidateNoPrototypeCleanupTargets<ConstructionCenter>(scenePath, issues);
        ValidateNoPrototypeCleanupTargets<UnitCombat>(scenePath, issues);
    }

    private static void ValidateNoPrototypeCleanupTargets<T>(string scenePath, List<string> issues)
        where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            GameObject target = ResolvePrototypeCleanupRoot(components[i]);
            if (target != null)
                issues.Add($"{scenePath}: unexpected preplaced {typeof(T).Name} on '{target.name}'.");
        }
    }

    private static GameObject FindSceneObject(string name)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        Scene activeScene = EditorSceneManager.GetActiveScene();

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null ||
                transform.name != name ||
                transform.gameObject.scene != activeScene)
            {
                continue;
            }

            return transform.gameObject;
        }

        return null;
    }

    private static void CreateSlotHeader(Transform parent)
    {
        GameObject row = CreateLayoutContainer(parent, "SlotHeaderRow");
        SetAnchors(row.GetComponent<RectTransform>(), 0.035f, 0.76f, 0.965f, 0.83f);
        CreateAnchoredTableLabel(row.transform, "Старт", 0f, 0f, 0.15f, 1f);
        CreateAnchoredTableLabel(row.transform, "Гравець", 0.17f, 0f, 0.42f, 1f);
        CreateAnchoredTableLabel(row.transform, "Команда", 0.45f, 0f, 0.67f, 1f);
        CreateAnchoredTableLabel(row.transform, "Складність", 0.70f, 0f, 0.94f, 1f);
    }

    private static void CreateSlotRowFixed(
        Transform parent,
        int index,
        List<TMP_Text> spawnLabels,
        List<TMP_Dropdown> controllerDropdowns,
        List<TMP_Dropdown> teamDropdowns,
        List<TMP_Dropdown> difficultyDropdowns)
    {
        float top = 0.72f - index * 0.14f;
        float bottom = top - 0.105f;
        GameObject row = CreateImagePanel(parent, "SlotRow_" + (index + 1), _genericSprite, new Color(0.05f, 0.17f, 0.29f, 0.78f), Image.Type.Sliced);
        SetAnchors(row.GetComponent<RectTransform>(), 0.035f, bottom, 0.965f, top);

        TMP_Text spawn = CreateAnchoredText(row.transform, "Spawn " + (index + 1), "Spawn " + (index + 1), 11f, TextAlignmentOptions.Left, 0.015f, 0.12f, 0.15f, 0.88f);
        ConfigureCompactText(spawn, 8f, 11f);

        TMP_Dropdown controller = CreateAnchoredDropdown(row.transform, "SlotController_" + (index + 1), 0.17f, 0.16f, 0.42f, 0.84f);
        TMP_Dropdown team = CreateAnchoredDropdown(row.transform, "SlotTeam_" + (index + 1), 0.45f, 0.16f, 0.67f, 0.84f);
        TMP_Dropdown difficulty = CreateAnchoredDropdown(row.transform, "SlotDifficulty_" + (index + 1), 0.70f, 0.16f, 0.94f, 0.84f);

        spawnLabels.Add(spawn);
        controllerDropdowns.Add(controller);
        teamDropdowns.Add(team);
        difficultyDropdowns.Add(difficulty);
    }

    private static void CreateMapRowButtonFixed(
        Transform parent,
        int index,
        List<Button> buttons,
        List<TMP_Text> labels)
    {
        float top = 0.62f - index * 0.13f;
        float bottom = top - 0.105f;
        Button button = CreateAnchoredButton(parent, "MapButton_" + (index + 1), "Map", 0.035f, bottom, 0.965f, top);
        TMP_Text label = button.transform.Find("Label").GetComponent<TMP_Text>();
        label.fontSize = 11f;
        label.alignment = TextAlignmentOptions.Left;
        ConfigureCompactText(label, 8f, 11f);
        Stretch(label.rectTransform, 0.035f, 0f, 0.965f, 1f);
        buttons.Add(button);
        labels.Add(label);
    }

    private static TMP_Text CreateAnchoredTableLabel(Transform parent, string label, float minX, float minY, float maxX, float maxY)
    {
        TMP_Text text = CreateAnchoredText(parent, label.Replace(" ", string.Empty) + "HeaderLabel", label, 10f, TextAlignmentOptions.Left, minX, minY, maxX, maxY);
        text.color = new Color(0.70f, 0.92f, 1f, 0.92f);
        ConfigureCompactText(text, 8f, 10f);
        return text;
    }

    private static void CreateSlotRow(
        Transform parent,
        int index,
        List<TMP_Text> spawnLabels,
        List<TMP_Dropdown> controllerDropdowns,
        List<TMP_Dropdown> teamDropdowns,
        List<TMP_Dropdown> difficultyDropdowns)
    {
        GameObject row = CreateImagePanel(parent, "SlotRow_" + (index + 1), _genericSprite, new Color(0.05f, 0.17f, 0.29f, 0.78f), Image.Type.Sliced);
        AddHorizontalLayout(row, 4, 2, 5);
        SetPreferredHeight(row, 30f);

        TMP_Text spawn = CreateTableLabel(row.transform, "Spawn " + (index + 1), 62f, 12f, new Color(0.92f, 0.98f, 1f, 1f));
        TMP_Dropdown controller = CreateDropdown(row.transform, "SlotController_" + (index + 1));
        TMP_Dropdown team = CreateDropdown(row.transform, "SlotTeam_" + (index + 1));
        TMP_Dropdown difficulty = CreateDropdown(row.transform, "SlotDifficulty_" + (index + 1));
        SetPreferredSize(controller.gameObject, 118f, 26f);
        SetPreferredSize(team.gameObject, 100f, 26f);
        SetPreferredSize(difficulty.gameObject, 100f, 26f);

        spawnLabels.Add(spawn);
        controllerDropdowns.Add(controller);
        teamDropdowns.Add(team);
        difficultyDropdowns.Add(difficulty);
    }

    private static void CreateMapRowButton(
        Transform parent,
        int index,
        List<Button> buttons,
        List<TMP_Text> labels)
    {
        Button button = CreateButton(parent, "MapButton_" + (index + 1), "Map");
        SetPreferredHeight(button.gameObject, 24f);
        TMP_Text label = button.transform.Find("Label").GetComponent<TMP_Text>();
        label.fontSize = 12f;
        label.alignment = TextAlignmentOptions.Left;
        ConfigureCompactText(label, 10f, 12f);
        Stretch(label.rectTransform, 0.04f, 0f, 0.96f, 1f);
        buttons.Add(button);
        labels.Add(label);
    }

    private static TMP_Text CreateTableLabel(Transform parent, string value, float width, float fontSize, Color color)
    {
        TMP_Text label = CreateText(parent, value.Replace(" ", string.Empty) + "Label", value, fontSize, TextAlignmentOptions.Left);
        label.color = color;
        ConfigureCompactText(label, Mathf.Max(8f, fontSize - 2f), fontSize);
        SetPreferredSize(label.gameObject, width, 18f);
        return label;
    }

    private static void EnsurePrototypeScenes()
    {
        EnsurePrototypeScene(PrototypeMapOneScenePath, 2, new[] { 2 }, 1);
        EnsurePrototypeScene(PrototypeMapTwoScenePath, 3, new[] { 3 }, 2);
        EnsurePrototypeScene(PrototypeMapThreeScenePath, 4, new[] { 2, 4 }, 3);
    }

    private static void EnsurePrototypeScene(string scenePath, int activeSpawns, IReadOnlyList<int> enabledPlayerCounts, int variant)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            AssetDatabase.CopyAsset(MainScenePath, scenePath);

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        ConfigurePrototypeStartPoints(activeSpawns, enabledPlayerCounts);
        CleanupPrototypeScene();
        RebuildSceneNavMesh();
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigurePrototypeStartPoints(int activeSpawns, IReadOnlyList<int> enabledPlayerCounts)
    {
        PlayerStartPoint[] points = Object.FindObjectsByType<PlayerStartPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(points, (first, second) => first.SlotIndex.CompareTo(second.SlotIndex));

        for (int i = 0; i < points.Length; i++)
        {
            PlayerStartPoint point = points[i];
            if (point == null)
                continue;

            point.gameObject.SetActive(i < activeSpawns);

            SerializedObject serialized = new(point);
            SetInt(serialized, "_slotIndex", i);
            SetIntArray(serialized, "_enabledForPlayerCounts", enabledPlayerCounts);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Vector3 position = point.transform.position;
            if (!Mathf.Approximately(position.y, StartPointGroundYOffset))
            {
                position.y = StartPointGroundYOffset;
                point.transform.position = position;
                EditorUtility.SetDirty(point.transform);
            }

            EditorUtility.SetDirty(point);
        }
    }

    private static void CleanupPrototypeScene()
    {
        GameObject decor = GameObject.Find("PrototypeMapVariantDecor");
        if (decor != null)
            Object.DestroyImmediate(decor);

        HashSet<GameObject> objectsToRemove = new();
        CollectPrototypeCleanupTargets<BuildingProduction>(objectsToRemove);
        CollectPrototypeCleanupTargets<BuildingHealth>(objectsToRemove);
        CollectPrototypeCleanupTargets<ConstructionCenter>(objectsToRemove);
        CollectPrototypeCleanupTargets<UnitCombat>(objectsToRemove);

        foreach (GameObject target in objectsToRemove)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
    }

    private static void CollectPrototypeCleanupTargets<T>(HashSet<GameObject> targets)
        where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            GameObject target = ResolvePrototypeCleanupRoot(components[i]);
            if (target != null)
                targets.Add(target);
        }
    }

    private static GameObject ResolvePrototypeCleanupRoot(Component component)
    {
        if (component == null)
            return null;

        GameObject candidate = PrefabUtility.GetOutermostPrefabInstanceRoot(component.gameObject);
        if (candidate == null)
            candidate = component.gameObject;

        // Outpost лишається capture point карти, навіть якщо має дочірню build-area-зону.
        if (candidate.GetComponentInParent<Outpost>(true) != null ||
            candidate.GetComponentInChildren<Outpost>(true) != null ||
            candidate.GetComponentInParent<PlayerStartPoint>(true) != null ||
            candidate.GetComponentInChildren<PlayerStartPoint>(true) != null)
        {
            return null;
        }

        return candidate;
    }

    private static void RebuildSceneNavMesh()
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < surfaces.Length; i++)
            surfaces[i].BuildNavMesh();
    }

    private static void UpdateMainScene(MapCatalog catalog, GameAssetRegistry registry)
    {
        EditorSceneManager.OpenScene(MainScenePath);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameObject systems = GameObject.Find("Match Systems") ?? new GameObject("Match Systems");

        SaveGameManager saveManager = GetOrAdd<SaveGameManager>(systems);
        GetOrAdd<MatchVictorySystem>(systems);
        SerializedObject saveSerialized = new(saveManager);
        SetObject(saveSerialized, "_registry", registry);
        SetObject(saveSerialized, "_mapCatalog", catalog);
        SetObject(saveSerialized, "_gridConfig", AssetDatabase.LoadAssetAtPath<BuildingPlacementGridConfig>("Assets/Balance/BuildingPlacementGridConfig.asset"));
        saveSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (canvas != null)
            ReplaceDebugPanel(canvas.transform, catalog);

        if (canvas != null && canvas.transform.Find("MatchResultPanel") == null)
            InstantiateUiPrefab(MatchResultPanelPrefabPath, "MatchResultPanel", canvas.transform);

        if (canvas != null && canvas.transform.Find("SaveGameToast") == null)
            CreateToast(canvas.transform);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    private static void ReplaceDebugPanel(Transform canvas, MapCatalog catalog)
    {
        Transform existing = canvas.Find("AiDebugPanel");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject panel = CreateVerticalPanel(canvas, "AiDebugPanel", 190f, 252f);
        AddVerticalLayout(panel, 10, 10, 6);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-16f, -16f);

        AiDebugPanelUI debug = panel.AddComponent<AiDebugPanelUI>();
        Button toggle = CreateButton(panel.transform, "ToggleButton", "AI: ON");
        SetPreferredHeight(toggle.gameObject, 34f);
        TMP_Dropdown mode = CreateDropdown(panel.transform, "DebugTeamMode");
        SetPreferredHeight(mode.gameObject, 30f);
        TMP_Dropdown difficulty = CreateDropdown(panel.transform, "DebugDifficulty");
        SetPreferredHeight(difficulty.gameObject, 30f);
        TMP_InputField resources = CreateInput(panel.transform, "DebugResources", "500");
        resources.text = "500";
        SetPreferredHeight(resources.gameObject, 30f);
        Button spawn = CreateButton(panel.transform, "SpawnMatchButton", "Spawn Match");
        SetPreferredHeight(spawn.gameObject, 36f);
        TMP_Text status = CreateText(panel.transform, "DebugStatus", string.Empty, 14f, TextAlignmentOptions.Center);
        SetPreferredHeight(status.gameObject, 28f);

        SerializedObject serialized = new(debug);
        SetObject(serialized, "_root", panel);
        SetObject(serialized, "_toggleButton", toggle);
        SetObject(serialized, "_spawnMatchButton", spawn);
        SetObject(serialized, "_teamModeDropdown", mode);
        SetObject(serialized, "_difficultyDropdown", difficulty);
        SetObject(serialized, "_startingResourcesInput", resources);
        SetObject(serialized, "_labelText", toggle.transform.Find("Label").GetComponent<TMP_Text>());
        SetObject(serialized, "_statusText", status);
        SetObject(serialized, "_mapCatalog", catalog);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateToast(Transform parent)
    {
        GameObject toast = CreatePanel(parent, "SaveGameToast", new Color(0.05f, 0.16f, 0.24f, 0.94f));
        RectTransform rect = toast.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.9f);
        rect.anchorMax = new Vector2(0.5f, 0.9f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280f, 44f);
        rect.anchoredPosition = Vector2.zero;

        SaveGameToastUI toastUi = toast.AddComponent<SaveGameToastUI>();
        TMP_Text text = CreateText(toast.transform, "Message", "Гру збережено", 18f, TextAlignmentOptions.Center);
        SerializedObject serialized = new(toastUi);
        SetObject(serialized, "_root", toast);
        SetObject(serialized, "_messageText", text);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void InstantiateUiPrefab(string prefabPath, string instanceName, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
            return;

        instance.name = instanceName;
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(MainScenePath, true),
            new EditorBuildSettingsScene(PrototypeMapOneScenePath, true),
            new EditorBuildSettingsScene(PrototypeMapTwoScenePath, true),
            new EditorBuildSettingsScene(PrototypeMapThreeScenePath, true)
        };
    }

    private static Camera CreateMenuCamera()
    {
        GameObject cameraObject = new("MainMenuCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.15f, 0.26f, 0.36f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = -10f;
        camera.farClipPlane = 10f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    private static Canvas CreateCanvas(string name)
    {
        GameObject canvasObject = new(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void LoadMenuStyleAssets()
    {
        _backgroundSprite = LoadSprite(SfBackgroundPath);
        _buttonSprite = LoadSprite(SfButtonPath);
        _genericSprite = LoadSprite(SfGenericPath);
        _windowSprite = LoadSprite(SfWindowPath);
        _menuFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JupiterFontPath);
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

    private static void CreateBackground(Transform root)
    {
        GameObject background = CreateImagePanel(root, "UnityUISamplesBackground", _backgroundSprite, new Color(0.98f, 1f, 1f, 0.96f), Image.Type.Simple);
        Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

        GameObject tint = CreatePanel(root, "LightBlueTint", new Color(0.10f, 0.22f, 0.34f, 0.16f));
        Stretch(tint.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

        GameObject bottomBand = CreatePanel(root, "BottomInterfaceBand", new Color(0.20f, 0.40f, 0.58f, 0.42f));
        SetAnchors(bottomBand.GetComponent<RectTransform>(), 0f, 0.08f, 1f, 0.82f);
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static GameObject CreateImagePanel(Transform parent, string name, Sprite sprite, Color color, Image.Type imageType)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null ? imageType : Image.Type.Simple;
        return panel;
    }

    private static GameObject CreateAnchoredContainer(Transform parent, string name, float minX, float minY, float maxX, float maxY)
    {
        GameObject container = new(name, typeof(RectTransform));
        container.transform.SetParent(parent, false);
        SetAnchors(container.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        return container;
    }

    private static GameObject CreateWindowPanel(Transform parent, string name, float minX, float minY, float maxX, float maxY)
    {
        GameObject window = CreateImagePanel(parent, name, _windowSprite, new Color(0.22f, 0.36f, 0.50f, 0.76f), Image.Type.Sliced);
        SetAnchors(window.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        return window;
    }

    private static GameObject CreateVerticalPanel(Transform parent, string name, float width, float height)
    {
        GameObject panel = CreateImagePanel(parent, name, _windowSprite, new Color(0.16f, 0.28f, 0.40f, 0.88f), Image.Type.Sliced);
        panel.AddComponent<VerticalLayoutGroup>();

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);

        AddVerticalLayout(panel, 18, 18, 10);
        return panel;
    }

    private static void AddVerticalLayout(GameObject target, int horizontalPadding, int verticalPadding, float spacing)
    {
        VerticalLayoutGroup layout = target.GetComponent<VerticalLayoutGroup>() ?? target.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private static void AddHorizontalLayout(GameObject target, int horizontalPadding, int verticalPadding, float spacing)
    {
        HorizontalLayoutGroup layout = target.GetComponent<HorizontalLayoutGroup>() ?? target.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static GameObject CreateLayoutContainer(Transform parent, string name)
    {
        GameObject container = new(name, typeof(RectTransform));
        container.transform.SetParent(parent, false);
        return container;
    }

    private static void CreateSectionHeader(Transform parent, string value)
    {
        TMP_Text header = CreateText(parent, value.Replace(" ", string.Empty) + "Header", value, 15f, TextAlignmentOptions.Left);
        header.fontStyle = FontStyles.Bold;
        header.color = new Color(0.64f, 0.94f, 1f, 1f);
        SetPreferredHeight(header.gameObject, 22f);
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject buttonObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = _buttonSprite;
        image.type = _buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.08f, 0.40f, 0.72f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        ApplyButtonColors(button, image.color);

        SetPreferredHeight(buttonObject, 38f);

        TMP_Text text = CreateText(buttonObject.transform, "Label", label, 14f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        ConfigureCompactText(text, 10f, 14f);
        Stretch(text.rectTransform, 0f, 0f, 1f, 1f);
        return button;
    }

    private static Button CreateAnchoredButton(Transform parent, string name, string label, float minX, float minY, float maxX, float maxY)
    {
        Button button = CreateButton(parent, name, label);
        SetAnchors(button.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        return button;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        Image image = button.GetComponent<Image>();
        image.color = color;
        ApplyButtonColors(button, color);
    }

    private static void ApplyButtonColors(Button button, Color normal)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = new Color(
            Mathf.Clamp01(normal.r + 0.18f),
            Mathf.Clamp01(normal.g + 0.18f),
            Mathf.Clamp01(normal.b + 0.18f),
            normal.a);
        colors.pressedColor = new Color(normal.r * 0.72f, normal.g * 0.72f, normal.b * 0.72f, normal.a);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.2f, 0.26f, 0.32f, 0.55f);
        button.colors = colors;
    }

    private static TMP_Dropdown CreateLabeledDropdown(Transform parent, string name, string label)
    {
        TMP_Text labelText = CreateText(parent, name + "Label", label, 11f, TextAlignmentOptions.Left);
        labelText.color = new Color(0.76f, 0.92f, 1f, 0.9f);
        SetPreferredHeight(labelText.gameObject, 13f);
        return CreateDropdown(parent, name);
    }

    private static TMP_InputField CreateLabeledInput(Transform parent, string name, string label, string placeholder)
    {
        TMP_Text labelText = CreateText(parent, name + "Label", label, 11f, TextAlignmentOptions.Left);
        labelText.color = new Color(0.76f, 0.92f, 1f, 0.9f);
        SetPreferredHeight(labelText.gameObject, 13f);
        return CreateInput(parent, name, placeholder);
    }

    private static TMP_Dropdown CreateDropdown(Transform parent, string name)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_Dropdown), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = _genericSprite;
        image.type = _genericSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.12f, 0.30f, 0.44f, 0.92f);

        SetPreferredHeight(root, 26f);

        TMP_Text label = CreateText(root.transform, "Label", string.Empty, 12f, TextAlignmentOptions.Left);
        label.color = new Color(0.96f, 0.99f, 1f, 1f);
        ConfigureCompactText(label, 9f, 12f);
        Stretch(label.rectTransform, 0.06f, 0f, 0.94f, 1f);

        RectTransform template = CreateDropdownTemplate(root.transform);
        TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
        dropdown.captionText = label;
        dropdown.template = template;
        dropdown.itemText = template.Find("Viewport/Content/Item/Item Label").GetComponent<TMP_Text>();
        return dropdown;
    }

    private static TMP_Dropdown CreateAnchoredDropdown(Transform parent, string name, float minX, float minY, float maxX, float maxY)
    {
        TMP_Dropdown dropdown = CreateDropdown(parent, name);
        SetAnchors(dropdown.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        return dropdown;
    }

    private static RectTransform CreateDropdownTemplate(Transform parent)
    {
        GameObject template = CreateImagePanel(parent, "Template", _windowSprite, new Color(0.10f, 0.24f, 0.36f, 0.98f), Image.Type.Sliced);
        template.AddComponent<ScrollRect>();

        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.sizeDelta = new Vector2(0f, 128f);

        GameObject viewport = new("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, 0f, 0f, 1f, 1f);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject item = new("Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        item.transform.SetParent(content.transform, false);
        item.GetComponent<Image>().color = new Color(0.12f, 0.30f, 0.44f, 1f);
        SetPreferredHeight(item, 24f);

        TMP_Text itemLabel = CreateText(item.transform, "Item Label", "Option", 12f, TextAlignmentOptions.Left);
        ConfigureCompactText(itemLabel, 9f, 12f);
        Stretch(itemLabel.rectTransform, 0.08f, 0f, 0.95f, 1f);

        Toggle toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = item.GetComponent<Image>();
        toggle.isOn = true;

        ScrollRect scroll = template.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        template.SetActive(false);
        return templateRect;
    }

    private static TMP_InputField CreateInput(Transform parent, string name, string placeholder)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = _genericSprite;
        image.type = _genericSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.12f, 0.30f, 0.44f, 0.92f);

        SetPreferredHeight(root, 26f);

        GameObject textArea = new("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(root.transform, false);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        Stretch(textAreaRect, 0.04f, 0.12f, 0.96f, 0.88f);

        TMP_Text text = CreateText(textArea.transform, "Text", string.Empty, 12f, TextAlignmentOptions.Left);
        TMP_Text hint = CreateText(textArea.transform, "Placeholder", placeholder, 12f, TextAlignmentOptions.Left);
        hint.color = new Color(1f, 1f, 1f, 0.50f);
        Stretch(text.rectTransform, 0f, 0f, 1f, 1f);
        Stretch(hint.rectTransform, 0f, 0f, 1f, 1f);

        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textViewport = textAreaRect;
        input.textComponent = text;
        input.placeholder = hint;
        return input;
    }

    private static TMP_InputField CreateAnchoredInput(Transform parent, string name, string placeholder, float minX, float minY, float maxX, float maxY)
    {
        TMP_InputField input = CreateInput(parent, name, placeholder);
        SetAnchors(input.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        return input;
    }

    private static Image CreatePreviewImage(Transform parent, string name, Sprite sprite)
    {
        GameObject preview = CreateImagePanel(parent, name, sprite, new Color(0.70f, 0.86f, 1f, 0.82f), Image.Type.Simple);
        Image image = preview.GetComponent<Image>();
        image.preserveAspect = false;
        SetPreferredHeight(preview, 60f);
        return image;
    }

    private static Image CreateAnchoredPreviewImage(Transform parent, string name, Sprite sprite, float minX, float minY, float maxX, float maxY)
    {
        Image image = CreatePreviewImage(parent, name, sprite);
        SetAnchors(image.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.98f, 1f, 1f);
        text.enableVertexGradient = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_Text CreateAnchoredText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment, float minX, float minY, float maxX, float maxY)
    {
        TMP_Text text = CreateText(parent, name, value, size, alignment);
        SetAnchors(text.rectTransform, minX, minY, maxX, maxY);
        ConfigureCompactText(text, Mathf.Max(7f, size - 3f), size);
        return text;
    }

    private static void ConfigureCompactText(TMP_Text text, float minSize, float maxSize)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void CreateFlexibleSpace(Transform parent, string name)
    {
        GameObject spacer = new(name, typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        LayoutElement layout = spacer.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 1f;
    }

    private static void SetPreferredHeight(GameObject target, float height)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    private static void SetPreferredSize(GameObject target, float width, float height)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    private static void SetFlexibleHeight(GameObject target, float flexibleHeight, float preferredHeight)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.flexibleHeight = flexibleHeight;
    }

    private static void SetFlexibleWidth(GameObject target, float flexibleWidth)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
        layout.flexibleWidth = flexibleWidth;
    }

    private static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        SetAnchors(rect, minX, minY, maxX, maxY);
    }

    private static void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void RebuildMenuLayout(Transform root)
    {
        Canvas.ForceUpdateCanvases();
        foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        Canvas.ForceUpdateCanvases();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            AssetDatabase.DeleteAsset(path);

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + folderName))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void SetRegistryArray(SerializedProperty property, IReadOnlyList<(string id, Object asset)> entries)
    {
        property.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("_id").stringValue = entries[i].id;
            element.FindPropertyRelative("_asset").objectReferenceValue = entries[i].asset;
        }
    }

    private static void SetObjectArray<T>(SerializedProperty property, IReadOnlyList<T> values)
        where T : Object
    {
        if (property == null)
            return;

        property.arraySize = values != null ? values.Count : 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetSupportedModes(SerializedProperty property, IReadOnlyList<SkirmishTeamMode> modes)
    {
        if (property == null)
            return;

        property.arraySize = modes != null ? modes.Count : 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).intValue = (int)modes[i];
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetInt(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetIntArray(SerializedObject serialized, string propertyName, IReadOnlyList<int> values)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            return;

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).intValue = values[i];
    }

    private static void SetEnumArray(SerializedObject serialized, string propertyName, IReadOnlyList<int> values)
    {
        SetIntArray(serialized, propertyName, values);
    }
}
