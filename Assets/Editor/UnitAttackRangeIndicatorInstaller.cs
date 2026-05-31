#if UNITY_EDITOR
using Strategy.Units;
using UnityEditor;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.UI;

/// <summary>
/// Editor-only tool that installs a UnitAttackRangeIndicator component onto tank unit prefabs
/// listed in <see cref="TankUnitDataPaths"/>. Invoked via Tools/RTS/Install Tank Range Indicators.
/// </summary>
public static class UnitAttackRangeIndicatorInstaller
{
    // UnitData asset paths for every tank variant that should receive a range indicator.
    private static readonly string[] TankUnitDataPaths =
    {
        "Assets/Balance/LightTank.asset",
        "Assets/Balance/MeadleTank.asset",
    };

    /// <summary>
    /// Iterates <see cref="TankUnitDataPaths"/>, resolves each UnitData's prefab path, and
    /// calls <see cref="InstallOnPrefab"/> to add or update the range indicator component.
    /// </summary>
    [MenuItem("Tools/RTS/Install Tank Range Indicators")]
    public static void InstallTankIndicators()
    {
        foreach (string unitDataPath in TankUnitDataPaths)
        {
            UnitData unitData = AssetDatabase.LoadAssetAtPath<UnitData>(unitDataPath);

            if (unitData == null || unitData.Prefab == null)
                continue;

            string prefabPath = AssetDatabase.GetAssetPath(unitData.Prefab);

            if (string.IsNullOrWhiteSpace(prefabPath))
                continue;

            InstallOnPrefab(prefabPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Tank attack range indicators installed.");
    }

    /// <summary>
    /// Loads the prefab at <paramref name="prefabPath"/>, adds a UnitAttackRangeIndicator if
    /// absent, sets its visual parameters (segments, line width, color, toggle mode), wires the
    /// UnitCombat reference, then saves the prefab.
    /// </summary>
    private static void InstallOnPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            UnitCombat combat = root.GetComponent<UnitCombat>();

            if (combat == null)
                return;

            UnitAttackRangeIndicator indicator = root.GetComponent<UnitAttackRangeIndicator>();

            if (indicator == null)
                indicator = root.AddComponent<UnitAttackRangeIndicator>();

            SetObjectReference(indicator, "_combat", combat);
            SetBool(indicator, "_showWhileSelected", false);
            SetBool(indicator, "_toggleWithKey", true);
            SetInt(indicator, "_segments", 128);
            SetFloat(indicator, "_lineWidth", 0.13f);
            SetFloat(indicator, "_heightOffset", 0.08f);
            SetColor(indicator, "_lineColor", new Color(0.14f, 0.85f, 1f, 0.95f));

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"Installed attack range indicator: {prefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Sets an Object reference serialized property on <paramref name="target"/> by name.</summary>
    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Sets a bool serialized property on <paramref name="target"/> by name.</summary>
    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Sets an int serialized property on <paramref name="target"/> by name.</summary>
    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Sets a float serialized property on <paramref name="target"/> by name.</summary>
    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Sets a Color serialized property on <paramref name="target"/> by name.</summary>
    private static void SetColor(Object target, string propertyName, Color value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.colorValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
