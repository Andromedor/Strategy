#if UNITY_EDITOR
using Strategy.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Налаштовує metadata юнітів, яку використовує SelectionInfoPanel: display name, fallback-код та icon.
/// Балансові характеристики не змінюються.
/// </summary>
public static class UnitSelectionMetadataInstaller
{
    private const string LightTankDataPath = "Assets/Balance/LightTank.asset";
    private const string MediumTankDataPath = "Assets/Balance/MeadleTank.asset";
    private const string ArtilleryDataPath = "Assets/Balance/SelfPropelledArtillery.asset";
    private const string ArtilleryProductionPath = "Assets/Balance/SelfPropelledArtilleryProduction.asset";
    private const string ArtilleryIconPath = "Assets/Models/SelfPropelledArtillery/SelfPropelledArtilleryPreview0001.png";

    [MenuItem("Tools/RTS/Install Unit Selection Metadata")]
    public static void Install()
    {
        Sprite artilleryIcon = LoadSpriteIcon(ArtilleryIconPath);

        ConfigureUnit(LightTankDataPath, "Quad Autocannon", null, "QA");
        ConfigureUnit(MediumTankDataPath, "Medium Tank", null, "MT");
        ConfigureUnit(ArtilleryDataPath, "Self-Propelled Artillery", artilleryIcon, "ART");
        ConfigureProductionIcon(ArtilleryProductionPath, artilleryIcon);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Unit selection metadata installed.");
    }

    private static void ConfigureUnit(string path, string displayName, Sprite icon, string fallbackText)
    {
        UnitData unitData = AssetDatabase.LoadAssetAtPath<UnitData>(path);

        if (unitData == null)
            return;

        SerializedObject serialized = new SerializedObject(unitData);
        SetString(serialized, "_displayName", displayName);
        SetString(serialized, "_selectionFallbackText", fallbackText);

        if (icon != null)
            SetObject(serialized, "_selectionIcon", icon);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(unitData);
    }

    private static void ConfigureProductionIcon(string path, Sprite icon)
    {
        if (icon == null)
            return;

        ProductionItemData production = AssetDatabase.LoadAssetAtPath<ProductionItemData>(path);

        if (production == null)
            return;

        SerializedObject serialized = new SerializedObject(production);
        SetObject(serialized, "_icon", icon);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(production);
    }

    private static Sprite LoadSpriteIcon(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null)
        {
            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }
}
#endif
