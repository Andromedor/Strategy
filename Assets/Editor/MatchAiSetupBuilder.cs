using System.Collections.Generic;
using Strategy.AI;
using Strategy.Buildings;
using Strategy.Core;
using Strategy.Data;
using Strategy.UI;
using Strategy.Units;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MatchAiSetupBuilder
{
    private const string ScenePath = "Assets/Scenes/mainScene.unity";
    private const string AiFolder = "Assets/Balance/AI";
    private const string EasyProfilePath = AiFolder + "/EasyAI.asset";
    private const string MediumProfilePath = AiFolder + "/MediumAI.asset";
    private const string HardProfilePath = AiFolder + "/HardAI.asset";
    private const string StartPointPrefabPath = "Assets/Prefabs/PlayerStartPoint.prefab";
    private const string AiDebugPanelPrefabPath = "Assets/Prefabs/UI/AiDebugPanel.prefab";
    private const string MatchResultPanelPrefabPath = "Assets/Prefabs/UI/MatchResultPanel.prefab";
    private const string MilitaryBaseDataPath = "Assets/Balance/MilitaryBase.asset";
    private const string FactoryDataPath = "Assets/Balance/HeavyFactory.asset";
    private const string GridConfigPath = "Assets/Balance/BuildingPlacementGridConfig.asset";
    private const string LightTankProductionPath = "Assets/Balance/LightTankProduction.asset";
    private const string MediumTankProductionPath = "Assets/Balance/MeadleTankProduction.asset";
    private const string ArtilleryProductionPath = "Assets/Balance/SelfPropelledArtilleryProduction.asset";
    private const float StartPointGroundYOffset = 0.05f;

    [MenuItem("Tools/RTS/Apply AI And Match Setup")]
    public static void Apply()
    {
        EnsureFolder("Assets/Balance", "AI");
        EnsureFolder("Assets/Prefabs", "UI");

        AiDifficultyProfile easy = LoadOrCreateProfile(EasyProfilePath, AiDifficultyLevel.Easy);
        AiDifficultyProfile medium = LoadOrCreateProfile(MediumProfilePath, AiDifficultyLevel.Medium);
        AiDifficultyProfile hard = LoadOrCreateProfile(HardProfilePath, AiDifficultyLevel.Hard);
        ConfigureProfile(easy, AiDifficultyLevel.Easy);
        ConfigureProfile(medium, AiDifficultyLevel.Medium);
        ConfigureProfile(hard, AiDifficultyLevel.Hard);

        CreateOrUpdateStartPointPrefab();
        CreateOrUpdateAiDebugPanelPrefab();
        CreateOrUpdateMatchResultPanelPrefab();

        EditorSceneManager.OpenScene(ScenePath);
        RemoveLegacyMilitaryBaseInstances();
        List<PlayerStartPoint> startPoints = EnsureSceneStartPoints();
        EnsureMatchSystems(startPoints, medium);
        EnsureHudPrefabsInScene();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static AiDifficultyProfile LoadOrCreateProfile(string path, AiDifficultyLevel difficulty)
    {
        AiDifficultyProfile profile = AssetDatabase.LoadAssetAtPath<AiDifficultyProfile>(path);
        if (profile != null)
            return profile;

        profile = AiDifficultyProfile.CreateRuntimeDefault(difficulty);
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static void ConfigureProfile(AiDifficultyProfile profile, AiDifficultyLevel difficulty)
    {
        ProductionItemData light = AssetDatabase.LoadAssetAtPath<ProductionItemData>(LightTankProductionPath);
        ProductionItemData medium = AssetDatabase.LoadAssetAtPath<ProductionItemData>(MediumTankProductionPath);
        ProductionItemData artillery = AssetDatabase.LoadAssetAtPath<ProductionItemData>(ArtilleryProductionPath);

        SerializedObject serialized = new(profile);
        SetEnum(serialized, "_difficulty", (int)difficulty);

        switch (difficulty)
        {
            case AiDifficultyLevel.Easy:
                SetFloat(serialized, "_decisionInterval", 2.5f);
                SetFloat(serialized, "_attackCooldown", 28f);
                SetFloat(serialized, "_captureCooldown", 12f);
                SetFloat(serialized, "_buildCooldown", 18f);
                SetInt(serialized, "_minimumOwnedOutposts", 1);
                SetInt(serialized, "_resourceReserve", 120);
                SetInt(serialized, "_desiredFactoryCount", 1);
                SetInt(serialized, "_captureSquadSize", 1);
                SetInt(serialized, "_attackGroupSize", 4);
                SetFloat(serialized, "_defenseRadius", 34f);
                SetInt(serialized, "_maxPendingWorkPerFactory", 1);
                SetBool(serialized, "_focusHighValueBuildings", false);
                SetProductionWeights(serialized, new[] { light, medium, artillery }, new[] { 1.2f, 0.7f, 0.15f });
                break;

            case AiDifficultyLevel.Hard:
                SetFloat(serialized, "_decisionInterval", 0.75f);
                SetFloat(serialized, "_attackCooldown", 12f);
                SetFloat(serialized, "_captureCooldown", 5f);
                SetFloat(serialized, "_buildCooldown", 8f);
                SetInt(serialized, "_minimumOwnedOutposts", 2);
                SetInt(serialized, "_resourceReserve", 60);
                SetInt(serialized, "_desiredFactoryCount", 2);
                SetInt(serialized, "_captureSquadSize", 3);
                SetInt(serialized, "_attackGroupSize", 8);
                SetFloat(serialized, "_defenseRadius", 55f);
                SetInt(serialized, "_maxPendingWorkPerFactory", 3);
                SetBool(serialized, "_focusHighValueBuildings", true);
                SetProductionWeights(serialized, new[] { light, medium, artillery }, new[] { 0.75f, 1.2f, 0.65f });
                break;

            default:
                SetFloat(serialized, "_decisionInterval", 1.5f);
                SetFloat(serialized, "_attackCooldown", 18f);
                SetFloat(serialized, "_captureCooldown", 8f);
                SetFloat(serialized, "_buildCooldown", 12f);
                SetInt(serialized, "_minimumOwnedOutposts", 1);
                SetInt(serialized, "_resourceReserve", 80);
                SetInt(serialized, "_desiredFactoryCount", 1);
                SetInt(serialized, "_captureSquadSize", 2);
                SetInt(serialized, "_attackGroupSize", 6);
                SetFloat(serialized, "_defenseRadius", 42f);
                SetInt(serialized, "_maxPendingWorkPerFactory", 2);
                SetBool(serialized, "_focusHighValueBuildings", false);
                SetProductionWeights(serialized, new[] { light, medium, artillery }, new[] { 1f, 1f, 0.35f });
                break;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void CreateOrUpdateStartPointPrefab()
    {
        GameObject root = new("PlayerStartPoint", typeof(PlayerStartPoint));
        root.transform.position = new Vector3(0f, StartPointGroundYOffset, 0f);

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(root.transform, false);
        pole.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        pole.transform.localScale = new Vector3(0.08f, 1.25f, 0.08f);

        GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.name = "Flag";
        flag.transform.SetParent(root.transform, false);
        flag.transform.localPosition = new Vector3(0.45f, 2.1f, 0f);
        flag.transform.localScale = new Vector3(0.85f, 0.42f, 0.04f);

        PrefabUtility.SaveAsPrefabAsset(root, StartPointPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateOrUpdateAiDebugPanelPrefab()
    {
        GameObject root = CreateUiRoot("AiDebugPanel", new Vector2(138f, 34f));
        AiDebugPanelUI panel = root.AddComponent<AiDebugPanelUI>();
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.02f, 0.07f, 0.1f, 0.86f);

        GameObject buttonObject = CreateUiChild(root.transform, "ToggleButton", Vector2.zero, Vector2.one);
        Button button = buttonObject.AddComponent<Button>();
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.02f, 0.45f, 0.85f, 0.95f);

        TMP_Text label = CreateText(buttonObject.transform, "Label", "AI: ON", 15f);

        SerializedObject serialized = new(panel);
        SetObject(serialized, "_root", root);
        SetObject(serialized, "_toggleButton", button);
        SetObject(serialized, "_labelText", label);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, AiDebugPanelPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateOrUpdateMatchResultPanelPrefab()
    {
        GameObject root = CreateUiRoot("MatchResultPanel", new Vector2(420f, 180f));
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.015f, 0.035f, 0.05f, 0.92f);
        MatchResultPanelUI panel = root.AddComponent<MatchResultPanelUI>();

        TMP_Text title = CreateText(root.transform, "Title", "Перемога", 44f);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.48f);
        titleRect.anchorMax = new Vector2(1f, 0.9f);
        titleRect.offsetMin = new Vector2(20f, 0f);
        titleRect.offsetMax = new Vector2(-20f, 0f);

        TMP_Text subtitle = CreateText(root.transform, "Subtitle", "Усі ворожі будівлі знищено.", 18f);
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0f, 0.16f);
        subtitleRect.anchorMax = new Vector2(1f, 0.46f);
        subtitleRect.offsetMin = new Vector2(20f, 0f);
        subtitleRect.offsetMax = new Vector2(-20f, 0f);

        SerializedObject serialized = new(panel);
        SetObject(serialized, "_root", root);
        SetObject(serialized, "_titleText", title);
        SetObject(serialized, "_subtitleText", subtitle);
        SetString(serialized, "_victoryTitle", "Перемога");
        SetString(serialized, "_defeatTitle", "Поразка");
        SetBool(serialized, "_returnToMainMenuAfterResult", true);
        SetFloat(serialized, "_returnDelaySeconds", 4f);
        SetString(serialized, "_mainMenuSceneName", "MainMenu");
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, MatchResultPanelPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static List<PlayerStartPoint> EnsureSceneStartPoints()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartPointPrefabPath);
        Vector3[] positions =
        {
            new(-58.4f, StartPointGroundYOffset, -37.4f),
            new(58.4f, StartPointGroundYOffset, 37.4f),
            new(-58.4f, StartPointGroundYOffset, 37.4f),
            new(58.4f, StartPointGroundYOffset, -37.4f)
        };
        TeamType[] previewTeams =
        {
            TeamType.Player,
            TeamType.Enemy,
            TeamType.Team3,
            TeamType.Team4
        };

        List<PlayerStartPoint> points = new();
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject pointObject = GameObject.Find("PlayerStart_" + (i + 1));
            if (pointObject == null)
            {
                pointObject = prefab != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                    : new GameObject("PlayerStart_" + (i + 1), typeof(PlayerStartPoint));
                pointObject.name = "PlayerStart_" + (i + 1);
            }

            pointObject.transform.position = positions[i];
            Vector3 toCenter = -positions[i];
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.01f)
                pointObject.transform.rotation = Quaternion.LookRotation(toCenter.normalized);

            PlayerStartPoint startPoint = pointObject.GetComponent<PlayerStartPoint>();
            SerializedObject serialized = new(startPoint);
            SetInt(serialized, "_slotIndex", i);
            SetEnum(serialized, "_editorPreviewTeam", (int)previewTeams[i]);
            SerializedProperty counts = serialized.FindProperty("_enabledForPlayerCounts");
            counts.arraySize = i < 2 ? 2 : 1;
            counts.GetArrayElementAtIndex(0).intValue = 4;
            if (i < 2)
                counts.GetArrayElementAtIndex(1).intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            points.Add(startPoint);
        }

        return points;
    }

    private static void EnsureMatchSystems(List<PlayerStartPoint> startPoints, AiDifficultyProfile mediumProfile)
    {
        GameObject systems = GameObject.Find("Match Systems");
        if (systems == null)
            systems = new GameObject("Match Systems");

        BuildingData militaryBase = AssetDatabase.LoadAssetAtPath<BuildingData>(MilitaryBaseDataPath);
        BuildingData factory = AssetDatabase.LoadAssetAtPath<BuildingData>(FactoryDataPath);
        BuildingPlacementGridConfig gridConfig = AssetDatabase.LoadAssetAtPath<BuildingPlacementGridConfig>(GridConfigPath);
        LayerMask blockMask = new() { value = 896 };

        MatchTeamSettings teamSettings = GetOrAdd<MatchTeamSettings>(systems);
        SerializedObject teamSettingsSerialized = new(teamSettings);
        SetEnum(teamSettingsSerialized, "_localTeam", (int)TeamType.Player);
        SetInt(teamSettingsSerialized, "_localPlayerId", 0);
        SerializedProperty teamsProperty = teamSettingsSerialized.FindProperty("_teams");
        teamsProperty.arraySize = 2;
        SerializedProperty playerSlot = teamsProperty.GetArrayElementAtIndex(0);
        playerSlot.FindPropertyRelative("_team").enumValueIndex = (int)TeamType.Player;
        playerSlot.FindPropertyRelative("_allianceId").intValue = 1;
        playerSlot.FindPropertyRelative("_controller").enumValueIndex = (int)TeamControllerKind.LocalHuman;
        playerSlot.FindPropertyRelative("_playerId").intValue = 0;
        SerializedProperty enemySlot = teamsProperty.GetArrayElementAtIndex(1);
        enemySlot.FindPropertyRelative("_team").enumValueIndex = (int)TeamType.Enemy;
        enemySlot.FindPropertyRelative("_allianceId").intValue = 2;
        enemySlot.FindPropertyRelative("_controller").enumValueIndex = (int)TeamControllerKind.AI;
        enemySlot.FindPropertyRelative("_playerId").intValue = 1;
        teamSettingsSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(teamSettings);

        MatchStartSpawner spawner = GetOrAdd<MatchStartSpawner>(systems);
        SerializedObject spawnerSerialized = new(spawner);
        SetObject(spawnerSerialized, "_startingBaseData", militaryBase);
        SetObject(spawnerSerialized, "_gridConfig", gridConfig);
        SetBool(spawnerSerialized, "_chargeStartingBaseCost", false);
        SerializedProperty pointsProperty = spawnerSerialized.FindProperty("_startPoints");
        pointsProperty.arraySize = startPoints.Count;
        for (int i = 0; i < startPoints.Count; i++)
            pointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = startPoints[i];
        spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        AiDirector director = GetOrAdd<AiDirector>(systems);
        SerializedObject directorSerialized = new(director);
        SetObject(directorSerialized, "_mediumProfile", mediumProfile);
        SetObject(directorSerialized, "_defaultProfile", mediumProfile);
        SetObject(directorSerialized, "_factoryData", factory);
        SetObject(directorSerialized, "_gridConfig", gridConfig);
        SetLayerMask(directorSerialized, "_buildingBlockMask", blockMask);
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        GetOrAdd<MatchVictorySystem>(systems);
    }

    private static void EnsureHudPrefabsInScene()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        EnsurePrefabInstance(AiDebugPanelPrefabPath, "AiDebugPanel", canvas.transform, new Vector2(0.92f, 0.98f), new Vector2(-82f, -20f));
        EnsurePrefabInstance(MatchResultPanelPrefabPath, "MatchResultPanel", canvas.transform, new Vector2(0.5f, 0.58f), Vector2.zero);
    }

    private static void EnsurePrefabInstance(
        string prefabPath,
        string objectName,
        Transform parent,
        Vector2 anchor,
        Vector2 anchoredPosition)
    {
        if (parent.Find(objectName) != null)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = objectName;
        RectTransform rect = instance.transform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
    }

    private static void RemoveLegacyMilitaryBaseInstances()
    {
        ConstructionCenter[] centers = Object.FindObjectsByType<ConstructionCenter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = centers.Length - 1; i >= 0; i--)
        {
            ConstructionCenter center = centers[i];
            if (center == null)
                continue;

            Transform root = center.transform.root;
            if (root != null && root.name.Contains("MilitaryBase"))
                Object.DestroyImmediate(root.gameObject);
        }
    }

    private static GameObject CreateUiRoot(string name, Vector2 size)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return root;
    }

    private static GameObject CreateUiChild(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return child;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize)
    {
        GameObject textObject = CreateUiChild(parent, name, Vector2.zero, Vector2.one);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetProductionWeights(
        SerializedObject serialized,
        IReadOnlyList<ProductionItemData> items,
        IReadOnlyList<float> weights)
    {
        SerializedProperty property = serialized.FindProperty("_productionWeights");
        property.arraySize = items.Count;

        for (int i = 0; i < items.Count; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("_item").objectReferenceValue = items[i];
            element.FindPropertyRelative("_weight").floatValue = weights[i];
        }
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

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetEnum(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.enumValueIndex = value;
    }

    private static void SetLayerMask(SerializedObject serialized, string propertyName, LayerMask value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value.value;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        string path = parent + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
