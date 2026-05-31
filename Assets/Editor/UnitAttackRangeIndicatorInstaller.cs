#if UNITY_EDITOR
using Strategy.Units;
using UnityEditor;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.UI;

/// <summary>
/// Редакторний інструмент, що встановлює компонент UnitAttackRangeIndicator на префаби танкових юнітів,
/// перелічені в <see cref="TankUnitDataPaths"/>. Викликається через Tools/RTS/Install Tank Range Indicators.
/// </summary>
public static class UnitAttackRangeIndicatorInstaller
{
    // Шляхи до асетів UnitData для кожного варіанту танка, що має отримати індикатор дальності.
    private static readonly string[] TankUnitDataPaths =
    {
        "Assets/Balance/LightTank.asset",
        "Assets/Balance/MeadleTank.asset",
    };

    /// <summary>
    /// Перебирає <see cref="TankUnitDataPaths"/>, визначає шлях до префабу кожного UnitData та
    /// викликає <see cref="InstallOnPrefab"/> для додавання або оновлення компонента індикатора дальності.
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
    /// Завантажує префаб за шляхом <paramref name="prefabPath"/>, додає UnitAttackRangeIndicator,
    /// якщо його немає, встановлює візуальні параметри (сегменти, ширина лінії, колір, режим перемикання),
    /// підключає посилання на UnitCombat, після чого зберігає префаб.
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

    /// <summary>Встановлює серіалізовану властивість-посилання на Object у <paramref name="target"/> за назвою.</summary>
    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Встановлює серіалізовану властивість типу bool у <paramref name="target"/> за назвою.</summary>
    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Встановлює серіалізовану властивість типу int у <paramref name="target"/> за назвою.</summary>
    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Встановлює серіалізовану властивість типу float у <paramref name="target"/> за назвою.</summary>
    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Встановлює серіалізовану властивість типу Color у <paramref name="target"/> за назвою.</summary>
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
