#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

using Strategy.Core;
using Strategy.UI;

/// <summary>
/// Редакторний інструмент, що повністю перебудовує префаб самохідної артилерійської установки (САУ) з нуля.
/// Викликається через меню Tools/RTS/Rebuild Self-Propelled Artillery. Створює префаб, призначає
/// всі компоненти, встановлює серіалізовані посилання та оновлює асети UnitData і ProductionItemData.
/// </summary>
public static class SelfPropelledArtilleryPrefabBuilder
{
    private const string ModelPath = "Assets/Models/SelfPropelledArtillery/SelfPropelledArtillery.fbx";
    private const string PrefabPath = "Assets/Prefabs/unit_SelfPropelledArtillery.prefab";
    private const string UnitDataPath = "Assets/Balance/SelfPropelledArtillery.asset";
    private const string ProductionItemPath = "Assets/Balance/SelfPropelledArtilleryProduction.asset";
    private const string ProductionConfigPath = "Assets/Balance/Factory Production Config.asset";
    private const string SelectionMaterialPath = "Assets/Material/SelectionVisual.mat";
    private const string MaterialFolderPath = "Assets/Models/SelfPropelledArtillery/Materials";

    // Значення кольору/шорсткості/металічності для кожного матеріалу за його точною назвою у FBX.
    private static readonly Dictionary<string, MaterialSettings> MaterialSettingsByName =
        new Dictionary<string, MaterialSettings>
        {
            { "SPG_Armor_Olive", new MaterialSettings(new Color(0.26f, 0.36f, 0.21f, 1f), 0.7f, 0f) },
            { "SPG_Armor_LightOlive", new MaterialSettings(new Color(0.43f, 0.52f, 0.32f, 1f), 0.68f, 0f) },
            { "SPG_Camo_Sand", new MaterialSettings(new Color(0.62f, 0.55f, 0.37f, 1f), 0.72f, 0f) },
            { "SPG_Camo_Brown", new MaterialSettings(new Color(0.25f, 0.19f, 0.12f, 1f), 0.78f, 0f) },
            { "SPG_Camo_DarkGreen", new MaterialSettings(new Color(0.12f, 0.22f, 0.12f, 1f), 0.75f, 0f) },
            { "SPG_Player_Blue", new MaterialSettings(new Color(0.04f, 0.18f, 0.86f, 1f), 0.48f, 0f) },
            { "SPG_Track_Dark", new MaterialSettings(new Color(0.035f, 0.034f, 0.032f, 1f), 0.76f, 0f) },
            { "SPG_Track_Pads", new MaterialSettings(new Color(0.12f, 0.115f, 0.105f, 1f), 0.84f, 0f) },
            { "SPG_Rubber", new MaterialSettings(new Color(0.08f, 0.075f, 0.07f, 1f), 0.8f, 0f) },
            { "SPG_Gun_Metal", new MaterialSettings(new Color(0.46f, 0.47f, 0.46f, 1f), 0.45f, 0.08f) },
            { "SPG_Dark_Metal", new MaterialSettings(new Color(0.18f, 0.18f, 0.17f, 1f), 0.55f, 0.12f) },
            { "SPG_Lamp_Amber", new MaterialSettings(new Color(1f, 0.65f, 0.13f, 1f), 0.3f, 0f) },
        };

    /// <summary>
    /// Точка входу для пункту меню. Будує префаб САУ з FBX-моделі, прикріплює всі
    /// необхідні компоненти, зберігає асет префабу та оновлює дані балансу й виробництва.
    /// </summary>
    [MenuItem("Tools/RTS/Rebuild Self-Propelled Artillery")]
    public static void Build()
    {
        ConfigureModelImporter();

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null)
            throw new System.InvalidOperationException($"Model was not imported: {ModelPath}");

        UnitData unitData = LoadOrCreate<UnitData>(UnitDataPath);
        ConfigureUnitData(unitData);

        GameObject root = new GameObject("unit_SelfPropelledArtillery");
        int playerLayer = LayerMask.NameToLayer("PlayerUnit");
        if (playerLayer >= 0)
            root.layer = playerLayer;

        TrySetTag(root, "Player");

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
        if (modelInstance == null)
            modelInstance = Object.Instantiate(modelPrefab);

        modelInstance.name = "SelfPropelledArtillery_Model";
        modelInstance.transform.SetParent(root.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        SetLayerRecursively(root, root.layer);
        ConfigureModelMaterials(modelInstance);

        Transform turret = FindChildRecursive(root.transform, "TurretPivot");
        Transform gun = FindChildRecursive(root.transform, "GunBarrel");
        Transform muzzle = FindChildRecursive(root.transform, "MuzzlePoint");

        if (turret != null)
            turret.localRotation = Quaternion.identity;

        if (gun != null)
            gun.localRotation = Quaternion.identity;

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.95f, 0f);
        collider.size = new Vector3(4.1f, 2.25f, 5.65f);

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 2.35f;
        agent.height = 3.2f;
        agent.baseOffset = -0.08f;
        agent.speed = 2.65f;
        agent.angularSpeed = 360f;
        agent.acceleration = 3.8f;
        agent.stoppingDistance = 3.6f;
        agent.autoBraking = true;
        agent.updatePosition = true;
        agent.updateRotation = false;

        ArtilleryWeapon combat = root.AddComponent<ArtilleryWeapon>();
        TankCannonEffects cannonEffects = root.AddComponent<TankCannonEffects>();
        TrackedVehicleMotor vehicleMotor = root.AddComponent<TrackedVehicleMotor>();
        TankTrackAnimator trackAnimator = root.AddComponent<TankTrackAnimator>();
        UnitSelectionState selectionState = root.AddComponent<UnitSelectionState>();
        ArtilleryRangeIndicator rangeIndicator = root.AddComponent<ArtilleryRangeIndicator>();
        TeamComponent teamComponent = root.AddComponent<TeamComponent>();
        UnitSpawnActivator spawnActivator = root.AddComponent<UnitSpawnActivator>();

        GameObject selectionVisual = CreateSelectionVisual(root.transform, root.layer);

        // Встановлення серіалізованих полів ArtilleryWeapon через SerializedObject без використання рефлексії.
        SetObjectReference(combat, "_unitData", unitData);
        SetObjectReference(combat, "_pointPosition", muzzle);
        SetObjectReference(combat, "_shotEffects", cannonEffects);
        SetObjectReference(combat, "_turret", turret);
        SetObjectReference(combat, "_gun", gun);

        SetObjectReference(cannonEffects, "_gun", gun);
        SetObjectReference(cannonEffects, "_muzzle", muzzle);
        SetFloat(cannonEffects, "_recoilDistance", 0.55f);
        SetInt(cannonEffects, "_flashParticles", 26);
        SetInt(cannonEffects, "_smokeParticles", 36);

        // Параметри прицілювання та точності, специфічні для артилерії.
        SetFloat(combat, "_minElevationAngle", 10f);
        SetFloat(combat, "_maxElevationAngle", 52f);
        SetFloat(combat, "_minElevationDistanceRatio", 0.08f);
        SetFloat(combat, "_elevationCurvePower", 2.1f);
        SetFloat(combat, "_maxRangeStationaryHitChance", 0.5f);
        SetFloat(combat, "_maxRangeMovingHitChance", 0.1f);
        SetFloat(combat, "_splashRadius", 4.6f);
        SetFloat(combat, "_maxMissRadius", 10f);

        ConfigureVehicleMotor(vehicleMotor, agent);
        SetObjectReference(trackAnimator, "_agent", agent);
        SetObjectReference(trackAnimator, "_vehicleMotor", vehicleMotor);
        SetInt(trackAnimator, "_segmentsPerRun", 30);
        SetInt(trackAnimator, "_endSegmentsPerLoop", 14);
        SetFloat(trackAnimator, "_trackHalfWidth", 2.29f);
        SetFloat(trackAnimator, "_trackLength", 5.12f);
        SetFloat(trackAnimator, "_trackCenterY", 0.48f);
        SetFloat(trackAnimator, "_trackVerticalSpacing", 0.78f);
        SetVector3(trackAnimator, "_segmentScale", new Vector3(0.15f, 0.09f, 0.22f));

        SetObjectReference(selectionState, "_selectionVisual", selectionVisual);
        SetObjectReference(rangeIndicator, "_combat", combat);
        SetInt(rangeIndicator, "_segments", 160);
        SetFloat(rangeIndicator, "_lineWidth", 0.16f);
        SetFloat(rangeIndicator, "_heightOffset", 0.08f);
        SetInt(teamComponent, "_team", (int)TeamType.Player);
        SetFloat(spawnActivator, "_exitMoveSpeed", 3.4f);
        SetFloat(spawnActivator, "_exitDistance", 0.25f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab == null)
            throw new System.InvalidOperationException($"Could not save prefab: {PrefabPath}");

        // Повторна конфігурація UnitData після збереження асету префабу.
        ConfigureUnitData(unitData, prefab);

        ProductionItemData productionItem = LoadOrCreate<ProductionItemData>(ProductionItemPath);
        productionItem.Configure("Artillery", unitData, 420, 12f);
        EditorUtility.SetDirty(productionItem);

        AddToFactoryConfig(productionItem);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Self-propelled artillery prefab rebuilt: {PrefabPath}");
    }

    /// <summary>
    /// Переконується, що імпортер FBX-моделі має вимкнену конвертацію осей, після чого примусово переімпортує.
    /// Запобігає неправильній орієнтації при інстанціюванні моделі в префаб.
    /// </summary>
    private static void ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;

        if (importer == null)
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return;
        }

        bool changed = false;

        if (importer.bakeAxisConversion)
        {
            importer.bakeAxisConversion = false;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
        else
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    /// <summary>
    /// Перебирає всі MeshRenderer на моделі та замінює їх матеріали на асети проекту,
    /// створені за таблицею <see cref="MaterialSettingsByName"/>, при необхідності створюючи нові асети матеріалів.
    /// </summary>
    private static void ConfigureModelMaterials(GameObject modelInstance)
    {
        EnsureFolder(MaterialFolderPath);

        foreach (MeshRenderer renderer in modelInstance.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;

            if (materials == null || materials.Length == 0)
                continue;

            for (int i = 0; i < materials.Length; i++)
            {
                string materialName = NormalizeMaterialName(materials[i] != null ? materials[i].name : renderer.name);
                materials[i] = LoadOrCreateMaterial(materialName);
            }

            renderer.sharedMaterials = materials;
        }
    }

    /// <summary>
    /// Завантажує асет матеріалу за назвою з теки матеріалів або створює новий з шейдером URP/Standard,
    /// якщо він ще не існує, після чого застосовує попередньо задані значення кольору/гладкості/металічності.
    /// </summary>
    private static Material LoadOrCreateMaterial(string materialName)
    {
        if (!MaterialSettingsByName.TryGetValue(materialName, out MaterialSettings settings))
            settings = new MaterialSettings(new Color(0.43f, 0.52f, 0.32f, 1f), 0.68f, 0f);

        string materialPath = $"{MaterialFolderPath}/{SanitizeFileName(materialName)}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Unlit/Color");

            material = new Material(shader)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        SetMaterialProperties(material, settings);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Застосовує колір, гладкість та металічність до матеріалу, перевіряючи наявність кожної властивості шейдера
    /// за назвою, щоб метод працював як з URP, так і з вбудованим рендер-пайплайном.
    /// </summary>
    private static void SetMaterialProperties(Material material, MaterialSettings settings)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", settings.Color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", settings.Color);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", settings.Smoothness);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", settings.Metallic);
    }

    /// <summary>
    /// Видаляє суфікс Unity " (Instance)" з назв матеріалів під час виконання,
    /// щоб вони збігались із ключами словника <see cref="MaterialSettingsByName"/>.
    /// </summary>
    private static string NormalizeMaterialName(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
            return "SPG_Armor_LightOlive";

        int instanceIndex = materialName.IndexOf(" (Instance)", System.StringComparison.Ordinal);

        if (instanceIndex >= 0)
            materialName = materialName.Substring(0, instanceIndex);

        return materialName.Trim();
    }

    /// <summary>
    /// Замінює символи, що є неприпустимими у назві файлу, на підкреслення.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalidChar, '_');

        return fileName;
    }

    /// <summary>
    /// Рекурсивно переконується, що всі батьківські теки для <paramref name="folderPath"/> існують,
    /// створюючи відсутні проміжні теки через AssetDatabase.
    /// </summary>
    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    /// <summary>
    /// Записує еталонні балансові значення в асет UnitData. Якщо <paramref name="prefab"/>
    /// вказано, також оновлює посилання на Prefab в асеті.
    /// </summary>
    private static void ConfigureUnitData(UnitData data, GameObject prefab = null)
    {
        data.Configure(
            prefab != null ? prefab : data.Prefab,
            135f,
            110f,
            28f,
            95f,
            8.5f,
            6.2f,
            35f,
            18f,
            -70f,
            8f,
            4f,
            3f,
            24f,
            "Self-Propelled Artillery",
            null,
            "ART");
        EditorUtility.SetDirty(data);
    }

    /// <summary>
    /// Встановлює всі серіалізовані поля TrackedVehicleMotor для налаштування руху САУ,
    /// прив'язуючи його до вказаного NavMeshAgent.
    /// </summary>
    private static void ConfigureVehicleMotor(TrackedVehicleMotor vehicleMotor, NavMeshAgent agent)
    {
        SetObjectReference(vehicleMotor, "_agent", agent);
        SetBool(vehicleMotor, "_applyAgentTuning", true);
        SetFloat(vehicleMotor, "_cruiseSpeed", 2.65f);
        SetFloat(vehicleMotor, "_acceleration", 3.8f);
        SetFloat(vehicleMotor, "_stoppingDistance", 3.6f);
        SetFloat(vehicleMotor, "_maxSteerAngle", 45f);
        SetFloat(vehicleMotor, "_steerResponsiveness", 7f);
        SetFloat(vehicleMotor, "_maxSteerSpeed", 120f);
        SetFloat(vehicleMotor, "_bodyTurnSpeed", 82f);
        SetFloat(vehicleMotor, "_sharpTurnSlowdownAngle", 78f);
        SetFloat(vehicleMotor, "_minimumForwardSpeed", 0.25f);
        SetFloat(vehicleMotor, "_alignmentStopAngle", 98f);
        SetFloat(vehicleMotor, "_alignmentResumeAngle", 42f);
        SetFloat(vehicleMotor, "_pivotTrackSpeed", 1.9f);
        SetFloat(vehicleMotor, "_minimumPivotTrackSpeed", 0.9f);
        SetFloat(vehicleMotor, "_curveDifferentialMultiplier", 0.62f);
        SetFloat(vehicleMotor, "_minimumCurveDifferentialSpeed", 0.28f);
        SetFloat(vehicleMotor, "_fullDifferentialAngle", 70f);
        SetFloat(vehicleMotor, "_trackResponse", 9f);
    }

    /// <summary>
    /// Створює дочірній GameObject-площину, що використовується як декаль підсвітки вибору юніта.
    /// Видаляє його MeshCollider, призначає матеріал виділення та починає як неактивний.
    /// </summary>
    private static GameObject CreateSelectionVisual(Transform parent, int layer)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
        visual.name = "SelectionVisual";
        visual.layer = layer;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = new Vector3(0f, 0.035f, 0f);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(0.62f, 1f, 0.62f);

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Material selectionMaterial = AssetDatabase.LoadAssetAtPath<Material>(SelectionMaterialPath);
        MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
        if (renderer != null && selectionMaterial != null)
            renderer.sharedMaterial = selectionMaterial;

        visual.SetActive(false);
        return visual;
    }

    /// <summary>
    /// Завантажує асет ScriptableObject за шляхом <paramref name="path"/> або створює та зберігає
    /// новий екземпляр, якщо такого асету не існує.
    /// </summary>
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>
    /// Реєструє <paramref name="productionItem"/> в головному асеті Factory Production Config,
    /// щоб він відображався у UI заводу під час гри.
    /// </summary>
    private static void AddToFactoryConfig(ProductionItemData productionItem)
    {
        ProductionConfig config = AssetDatabase.LoadAssetAtPath<ProductionConfig>(ProductionConfigPath);
        if (config == null)
            return;

        config.AddItem(productionItem);
        EditorUtility.SetDirty(config);
    }

    /// <summary>
    /// Пошук у глибину дочірнього Transform із заданою назвою в будь-якому місці ієрархії.
    /// </summary>
    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, name);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Рекурсивно призначає <paramref name="layer"/> об'єкту <paramref name="target"/> та всім його дочірнім об'єктам.
    /// </summary>
    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>
    /// Встановлює тег на <paramref name="target"/>, повертаючись до "Untagged", якщо тег
    /// не визначений у менеджері тегів проекту.
    /// </summary>
    private static void TrySetTag(GameObject target, string tagName)
    {
        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
            target.tag = "Untagged";
        }
    }

    /// <summary>
    /// Встановлює серіалізовану властивість-посилання на Object на <paramref name="target"/> за назвою властивості,
    /// використовуючи SerializedObject, щоб зміна відстежувалась AssetDatabase без скасування.
    /// </summary>
    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Встановлює серіалізовану властивість типу float на <paramref name="target"/> за назвою властивості.
    /// </summary>
    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Встановлює серіалізовану властивість типу bool на <paramref name="target"/> за назвою властивості.
    /// </summary>
    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Встановлює серіалізовану властивість типу Vector3 на <paramref name="target"/> за назвою властивості.
    /// </summary>
    private static void SetVector3(Object target, string propertyName, Vector3 value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.vector3Value = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Встановлює серіалізовану властивість типу int на <paramref name="target"/> за назвою властивості.
    /// </summary>
    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Тип-значення, що зберігає налаштування PBR-поверхні для одного іменованого слоту матеріалу.</summary>
    private readonly struct MaterialSettings
    {
        public MaterialSettings(Color color, float smoothness, float metallic)
        {
            Color = color;
            Smoothness = smoothness;
            Metallic = metallic;
        }

        public Color Color { get; }
        public float Smoothness { get; }
        public float Metallic { get; }
    }
}
#endif
