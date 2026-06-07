using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    /// <summary>
    /// ScriptableObject, що зберігає всі бойові, рухові та прицільні характеристики одного типу юніта.
    /// Використовується UnitCombat, виробництвом на заводі та редакторними конфігураторами префабів.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitData", menuName = "RTS/UnitData")]
    public class UnitData : ScriptableObject
    {
        [Header("Production")]
        [SerializeField, FormerlySerializedAs("Prefab")] private GameObject _prefab;
        [SerializeField, FormerlySerializedAs("DisplayName")] private string _displayName;
        [SerializeField, FormerlySerializedAs("SelectionIcon")] private Sprite _selectionIcon;
        [SerializeField, FormerlySerializedAs("SelectionFallbackText")] private string _selectionFallbackText;

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
        [SerializeField, Min(0f)] private float _idleScanDelay = 5f;
        [SerializeField, Min(0f)] private float _idleScanIntervalMin = 2.5f;
        [SerializeField, Min(0f)] private float _idleScanIntervalMax = 6f;
        [SerializeField, Range(0f, 180f)] private float _idleScanYawRange = 45f;
        [SerializeField] private bool _opportunisticTargeting = true;

        public GameObject Prefab => _prefab;
        public string DisplayName => _displayName;
        public Sprite SelectionIcon => _selectionIcon;
        public string SelectionFallbackText => _selectionFallbackText;
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
        public float IdleScanDelay => _idleScanDelay;
        public float IdleScanIntervalMin => _idleScanIntervalMin;
        public float IdleScanIntervalMax => Mathf.Max(_idleScanIntervalMin, _idleScanIntervalMax);
        public float IdleScanYawRange => _idleScanYawRange;
        public bool OpportunisticTargeting => _opportunisticTargeting;

        /// <summary>
        /// Записує всі характеристики юніта за один виклик; використовується редакторними скриптами
        /// для встановлення еталонних балансових значень без ручного редагування в Інспекторі.
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
            float idleTurretRotationSpeed,
            string displayName = null,
            Sprite selectionIcon = null,
            string selectionFallbackText = null)
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

            if (!string.IsNullOrWhiteSpace(displayName))
                _displayName = displayName;

            if (selectionIcon != null)
                _selectionIcon = selectionIcon;

            if (!string.IsNullOrWhiteSpace(selectionFallbackText))
                _selectionFallbackText = selectionFallbackText;
        }

        private void OnValidate()
        {
            _idleScanDelay = Mathf.Max(0f, _idleScanDelay);
            _idleScanIntervalMin = Mathf.Max(0f, _idleScanIntervalMin);
            _idleScanIntervalMax = Mathf.Max(_idleScanIntervalMin, _idleScanIntervalMax);
            _idleScanYawRange = Mathf.Clamp(_idleScanYawRange, 0f, 180f);
        }
    }
}
