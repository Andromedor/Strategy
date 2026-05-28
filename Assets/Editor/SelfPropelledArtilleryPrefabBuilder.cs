#if UNITY_EDITOR
using System.Collections.Generic;
using Building_and_creat_Uniit;
using Data;
using UnitController;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class SelfPropelledArtilleryPrefabBuilder
{
    private const string ModelPath = "Assets/Models/SelfPropelledArtillery/SelfPropelledArtillery.fbx";
    private const string PrefabPath = "Assets/Prefabs/unit_SelfPropelledArtillery.prefab";
    private const string UnitDataPath = "Assets/Balance/SelfPropelledArtillery.asset";
    private const string ProductionItemPath = "Assets/Balance/SelfPropelledArtilleryProduction.asset";
    private const string ProductionConfigPath = "Assets/Balance/Factory Production Config.asset";
    private const string SelectionMaterialPath = "Assets/Material/SelectionVisual.mat";

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
        modelInstance.transform.localScale = Vector3.one;

        SetLayerRecursively(root, root.layer);

        Transform turret = FindChildRecursive(root.transform, "TurretPivot");
        Transform gun = FindChildRecursive(root.transform, "GunBarrel");
        Transform muzzle = FindChildRecursive(root.transform, "MuzzlePoint");

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
        agent.acceleration = 7f;
        agent.stoppingDistance = 3.6f;

        UnitCombat combat = root.AddComponent<UnitCombat>();
        ArtilleryWeapon artilleryWeapon = root.AddComponent<ArtilleryWeapon>();
        TankCannonEffects cannonEffects = root.AddComponent<TankCannonEffects>();
        TankTrackAnimator trackAnimator = root.AddComponent<TankTrackAnimator>();
        UnitSelectionState selectionState = root.AddComponent<UnitSelectionState>();
        TeamComponent teamComponent = root.AddComponent<TeamComponent>();
        UnitSpawnActivator spawnActivator = root.AddComponent<UnitSpawnActivator>();

        GameObject selectionVisual = CreateSelectionVisual(root.transform, root.layer);

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

        SetFloat(artilleryWeapon, "_maxElevationAngle", 70f);
        SetFloat(artilleryWeapon, "_maxRangeStationaryHitChance", 0.5f);
        SetFloat(artilleryWeapon, "_maxRangeMovingHitChance", 0.1f);
        SetFloat(artilleryWeapon, "_splashRadius", 4.6f);
        SetFloat(artilleryWeapon, "_maxMissRadius", 10f);

        SetObjectReference(trackAnimator, "_agent", agent);
        SetInt(trackAnimator, "_segmentsPerRun", 30);
        SetInt(trackAnimator, "_endSegmentsPerLoop", 14);
        SetFloat(trackAnimator, "_trackHalfWidth", 2.29f);
        SetFloat(trackAnimator, "_trackLength", 5.12f);
        SetFloat(trackAnimator, "_trackCenterY", 0.48f);
        SetFloat(trackAnimator, "_trackVerticalSpacing", 0.78f);
        SetVector3(trackAnimator, "_segmentScale", new Vector3(0.15f, 0.09f, 0.22f));

        SetObjectReference(selectionState, "_selectionVisual", selectionVisual);
        SetInt(teamComponent, "_team", (int)TeamType.Player);
        SetFloat(spawnActivator, "_exitMoveSpeed", 3.4f);
        SetFloat(spawnActivator, "_exitDistance", 0.25f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab == null)
            throw new System.InvalidOperationException($"Could not save prefab: {PrefabPath}");

        unitData.Prefab = prefab;
        EditorUtility.SetDirty(unitData);

        ProductionItemData productionItem = LoadOrCreate<ProductionItemData>(ProductionItemPath);
        productionItem.ItemName = "Artillery";
        productionItem.UnitData = unitData;
        productionItem.Cost = 420;
        productionItem.ProductionTime = 12f;
        EditorUtility.SetDirty(productionItem);

        AddToFactoryConfig(productionItem);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Self-propelled artillery prefab rebuilt: {PrefabPath}");
    }

    private static void ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;

        if (importer == null)
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return;
        }

        bool changed = false;

        if (!importer.bakeAxisConversion)
        {
            importer.bakeAxisConversion = true;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
        else
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureUnitData(UnitData data)
    {
        data.MaxHealth = 135f;
        data.Damage = 110f;
        data.Speed = 28f;
        data.AttackRange = 95f;
        data.AttackDelay = 6.5f;
        data.FormationSpacing = 6.2f;
        data.TurretRotationSpeed = 70f;
        data.GunPitchSpeed = 38f;
        data.MinGunPitch = -70f;
        data.MaxGunPitch = 8f;
        data.AimAngleTolerance = 4f;
        data.ReturnTurretDelay = 3f;
        data.IdleTurretRotationSpeed = 45f;
        EditorUtility.SetDirty(data);
    }

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

    private static void AddToFactoryConfig(ProductionItemData productionItem)
    {
        ProductionConfig config = AssetDatabase.LoadAssetAtPath<ProductionConfig>(ProductionConfigPath);
        if (config == null)
            return;

        if (config.Items == null)
            config.Items = new List<ProductionItemData>();

        if (!config.Items.Contains(productionItem))
        {
            config.Items.Add(productionItem);
            EditorUtility.SetDirty(config);
        }
    }

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

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

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

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVector3(Object target, string propertyName, Vector3 value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.vector3Value = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
