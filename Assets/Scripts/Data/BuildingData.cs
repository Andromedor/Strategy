using UnityEngine;

[CreateAssetMenu(fileName = "BuildingData", menuName = "RTS/BuildingData")]
public class BuildingData : ScriptableObject
{
    [Header("Prefab")]
    public GameObject  prefab;
    
    [Header("Economy")]
    public int economy;
    
    [Header("Construction")]
    public float BuildTime;
    
    [Header("Placement")]
    public Vector3 CheckBoxSize;
    public Vector3 CheckBoxOffset;
}
