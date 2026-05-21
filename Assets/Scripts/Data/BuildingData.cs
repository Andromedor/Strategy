using UnityEngine;

[CreateAssetMenu(fileName = "BuildingData", menuName = "RTS/BuildingData")]
public class BuildingData : ScriptableObject
{
    [Header("Info")]
    public string BuildingName;
    // Назва будівлі.

    public Sprite Icon;
    // Іконка для UI.
    
    [Header("Prefab")]
    public GameObject  prefab;
    
    [Header("Economy")]
    public int economy;
    
    [Header("Construction")]
    public float BuildTime;
    public float MaxHealth;
    
    [Header("Placement")]
    public Vector3 CheckBoxSize;
    public Vector3 CheckBoxOffset;
    
    [Header("UI")]
    public PanelType PanelType;
}
