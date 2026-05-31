using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    /// <summary>
    /// ScriptableObject that holds all combat, movement, and aiming stats for a single unit type.
    /// Referenced by UnitCombat, factory production, and editor prefab builders.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitData", menuName = "RTS/UnitData")]
    public class UnitData : ScriptableObject
    {
        [Header("Production")]
        [SerializeField, FormerlySerializedAs("Prefab")] private GameObject _prefab;

        [Header("Combat")]
        [SerializeField, FormerlySerializedAs("MaxHealth")] private float _maxHealth = 100f;
        [SerializeField, FormerlySerializedAs("Damage")] private float _damage = 10f;
        [SerializeField, FormerlySerializedAs("Speed")] private float _speed = 2f;
        [SerializeField, FormerlySerializedAs("AttackRange")] private float _attackRange = 20f;
        [SerializeField, FormerlySerializedAs("AttackDelay")] private float _attackDelay = 2f;

        [Header("Movement")]
        [SerializeField, FormerlySerializedAs("FormationSpacing")] private float _formationSpacing = 4f;

        [Header("Aiming")]
        [SerializeField, FormerlySerializedAs("TurretRotationSpeed")] private float _turretRotationSpeed = 180f;
        [SerializeField, FormerlySerializedAs("GunPitchSpeed")] private float _gunPitchSpeed = 90f;
        [SerializeField, FormerlySerializedAs("MinGunPitch")] private float _minGunPitch = -5f;
        [SerializeField, FormerlySerializedAs("MaxGunPitch")] private float _maxGunPitch = 20f;
        [SerializeField, FormerlySerializedAs("AimAngleTolerance")] private float _aimAngleTolerance = 3f;
        [SerializeField, FormerlySerializedAs("ReturnTurretDelay")] private float _returnTurretDelay = 2f;
        [SerializeField, FormerlySerializedAs("IdleTurretRotationSpeed")] private float _idleTurretRotationSpeed = 90f;

        public GameObject Prefab => _prefab;
        public float MaxHealth => _maxHealth;
        public float Damage => _damage;
        public float Speed => _speed;
        public float AttackRange => _attackRange;
        public float AttackDelay => _attackDelay;
        public float FormationSpacing => _formationSpacing;
        public float TurretRotationSpeed => _turretRotationSpeed;
        public float GunPitchSpeed => _gunPitchSpeed;
        public float MinGunPitch => _minGunPitch;
        public float MaxGunPitch => _maxGunPitch;
        public float AimAngleTolerance => _aimAngleTolerance;
        public float ReturnTurretDelay => _returnTurretDelay;
        public float IdleTurretRotationSpeed => _idleTurretRotationSpeed;

        /// <summary>
        /// Writes all unit stats at once; used by editor prefab builder scripts to set
        /// canonical balance values without requiring manual Inspector edits.
        /// </summary>
        public void Configure(
            GameObject prefab,
            float maxHealth,
            float damage,
            float speed,
            float attackRange,
            float attackDelay,
            float formationSpacing,
            float turretRotationSpeed,
            float gunPitchSpeed,
            float minGunPitch,
            float maxGunPitch,
            float aimAngleTolerance,
            float returnTurretDelay,
            float idleTurretRotationSpeed)
        {
            _prefab = prefab;
            _maxHealth = maxHealth;
            _damage = damage;
            _speed = speed;
            _attackRange = attackRange;
            _attackDelay = attackDelay;
            _formationSpacing = formationSpacing;
            _turretRotationSpeed = turretRotationSpeed;
            _gunPitchSpeed = gunPitchSpeed;
            _minGunPitch = minGunPitch;
            _maxGunPitch = maxGunPitch;
            _aimAngleTolerance = aimAngleTolerance;
            _returnTurretDelay = returnTurretDelay;
            _idleTurretRotationSpeed = idleTurretRotationSpeed;
        }
    }
}
