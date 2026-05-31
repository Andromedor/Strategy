#if UNITY_EDITOR
using Strategy.Units;
using UnityEditor;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.UI;
public static class UnitAttackRangeIndicatorInstaller
{
    private static readonly string[] TankUnitDataPaths =
    {
        "Assets/Balance/LightTank.asset",
        "Assets/Balance/MeadleTank.asset",
    };

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

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.boolValue = value;
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

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

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
