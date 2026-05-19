using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "RTS/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("Production")]
    public GameObject Prefab;
    public int Cost;
    public float ProductionTime;
    
    [Header("Combat")]
    public float MaxHealth = 100f;
    public float Damage = 10f;
    public float Speed = 2f;
    public float AttackRange = 20f;
    public float AttackDelay = 2f;
    [Header("Movement")]
    public float FormationSpacing = 4f;
}
