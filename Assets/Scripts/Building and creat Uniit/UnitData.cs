using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "RTS/UnitData")]
public class UnitData : ScriptableObject
{
    public GameObject Prefab;
    public int Cost;
    public float ProductionTime;
}
