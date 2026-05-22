using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "RTS/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("Production")]
    public GameObject Prefab;
    
    [Header("Combat")]
    public float MaxHealth = 100f;
    public float Damage = 10f;
    public float Speed = 2f;
    public float AttackRange = 20f;
    public float AttackDelay = 2f;
    [Header("Movement")]
    public float FormationSpacing = 4f;
    
    [Header("Aiming")]
    public float TurretRotationSpeed = 180f;
// Швидкість повороту башні по Y.

    public float GunPitchSpeed = 90f;
// Швидкість підйому/опускання пушки.

    public float MinGunPitch = -5f;
// Мінімальний кут пушки вниз.

    public float MaxGunPitch = 20f;
// Максимальний кут пушки вгору.

    public float AimAngleTolerance = 3f;
// Наскільки точно треба навестись, щоб дозволити постріл.

    public float ReturnTurretDelay = 2f;
// Через скільки секунд без target
// башня починає повертатись назад.

    public float IdleTurretRotationSpeed = 90f;
// Швидкість повернення в idle.
}
