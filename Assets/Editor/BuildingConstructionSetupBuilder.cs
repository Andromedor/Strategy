using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Data;
using UnityEditor;
using UnityEngine;

public static class BuildingConstructionSetupBuilder
{
    private const string FactoryPrefabPath = "Assets/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_2/prefabs_yup/struct_Factory_Heavy_A_yup.prefab";
    private const string MilitaryBasePrefabPath = "Assets/CartoonMilitaryModelPack/Prefebs/Building_Prefebs/MilitaryBase_Prefeb.prefab";
    private const string HeavyFactoryDataPath = "Assets/Balance/HeavyFactory.asset";
    private const string MilitaryBaseDataPath = "Assets/Balance/MilitaryBase.asset";
    private const float DefaultBuildTimeSeconds = 10f;

    [MenuItem("Tools/RTS/Apply Building Construction Setup")]
    public static void Apply()
    {
        ConfigureBuildTime(HeavyFactoryDataPath, DefaultBuildTimeSeconds);
        ConfigureBuildTime(MilitaryBaseDataPath, DefaultBuildTimeSeconds);

        ConfigurePrefab(
            FactoryPrefabPath,
            new Vector3(0f, 2.15f, 1.4f),
            new Vector3(9.5f, 3.2f, 10.5f));

        ConfigurePrefab(
            MilitaryBasePrefabPath,
            new Vector3(0f, 3.2f, 0f),
            new Vector3(13.5f, 4.5f, 13.5f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ConfigureBuildTime(string assetPath, float buildTime)
    {
        BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath);
        if (data == null)
            return;

        SerializedObject serialized = new(data);
        SerializedProperty property = serialized.FindProperty("_buildTime");
        if (property != null)
            property.floatValue = Mathf.Max(0f, buildTime);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static void ConfigurePrefab(
        string prefabPath,
        Vector3 effectLocalPosition,
        Vector3 effectBoxScale)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            BuildingConstructionState construction = prefabRoot.GetComponent<BuildingConstructionState>();
            if (construction == null)
                construction = prefabRoot.AddComponent<BuildingConstructionState>();

            RemoveConstructionStatusUi(prefabRoot);

            BuildingConstructionVisual visual = prefabRoot.GetComponent<BuildingConstructionVisual>();
            if (visual == null)
                visual = prefabRoot.AddComponent<BuildingConstructionVisual>();

            GameObject effectRoot = EnsureEffectRoot(prefabRoot.transform, effectLocalPosition, effectBoxScale);
            ParticleSystem particles = EnsureAssemblyParticles(effectRoot.transform, effectBoxScale);
            List<Renderer> renderers = CollectAssemblyRenderers(prefabRoot.transform, effectRoot.transform);

            SerializedObject serialized = new(visual);
            SetObject(serialized, "_construction", construction);
            SetObject(serialized, "_visualRoot", prefabRoot.transform);
            SetObject(serialized, "_effectRoot", effectRoot);
            SetInt(serialized, "_initialVisiblePartCount", 1);
            SetInt(serialized, "_maxStageCount", 60);
            SetVector3(serialized, "_partStartLocalOffset", new Vector3(0f, -1.15f, 0f));
            SetFloat(serialized, "_partStartScale", 0.9f);
            SetFloat(serialized, "_partRevealStageSpan", 1f);
            SetObjectArray(serialized.FindProperty("_assemblyRenderers"), renderers);
            SetObjectArray(serialized.FindProperty("_constructionParticles"), new Object[] { particles });
            serialized.ApplyModifiedPropertiesWithoutUndo();

            effectRoot.SetActive(false);
            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void RemoveConstructionStatusUi(GameObject prefabRoot)
    {
        BuildingConstructionStatusPresenter presenter = prefabRoot.GetComponent<BuildingConstructionStatusPresenter>();
        if (presenter != null)
            Object.DestroyImmediate(presenter, true);

        Transform statusBar = FindChildRecursive(prefabRoot.transform, "BuildingConstructionStatusBar");
        if (statusBar != null)
            Object.DestroyImmediate(statusBar.gameObject);
    }

    private static GameObject EnsureEffectRoot(Transform parent, Vector3 localPosition, Vector3 boxScale)
    {
        Transform existing = parent.Find("ConstructionAssemblyFx");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject("ConstructionAssemblyFx");

        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Light light = root.GetComponent<Light>();
        if (light == null)
            light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.25f, 0.85f, 1f, 1f);
        light.range = Mathf.Max(boxScale.x, boxScale.z) * 0.8f;
        light.intensity = 1.15f;

        return root;
    }

    private static ParticleSystem EnsureAssemblyParticles(Transform parent, Vector3 boxScale)
    {
        Transform existing = parent.Find("AssemblySparks");
        GameObject sparks = existing != null
            ? existing.gameObject
            : new GameObject("AssemblySparks");

        sparks.transform.SetParent(parent, false);
        sparks.transform.localPosition = Vector3.zero;
        sparks.transform.localRotation = Quaternion.identity;
        sparks.transform.localScale = Vector3.one;

        ParticleSystem particles = sparks.GetComponent<ParticleSystem>();
        if (particles == null)
            particles = sparks.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.18f, 0.95f, 1f, 0.95f),
            new Color(1f, 1f, 1f, 0.85f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 140;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 34f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxScale;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            renderer = sparks.AddComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 10;
        renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");

        return particles;
    }

    private static List<Renderer> CollectAssemblyRenderers(Transform root, Transform effectRoot)
    {
        Renderer[] allRenderers = root.GetComponentsInChildren<Renderer>(true);
        List<Renderer> renderers = new(allRenderers.Length);

        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer renderer = allRenderers[i];
            if (renderer == null ||
                renderer.GetComponentInParent<Canvas>() != null ||
                (effectRoot != null && renderer.transform.IsChildOf(effectRoot)))
            {
                continue;
            }

            renderers.Add(renderer);
        }

        return renderers;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if (child.name == name)
                return child;

            Transform nested = FindChildRecursive(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetObjectArray(SerializedProperty property, IReadOnlyList<Object> values)
    {
        if (property == null)
            return;

        property.arraySize = values != null ? values.Count : 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetInt(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetVector3(SerializedObject serialized, string propertyName, Vector3 value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }
}
