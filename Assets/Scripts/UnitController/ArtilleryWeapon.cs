using System.Collections;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Artillery-specific subclass of UnitCombat. Overrides gun elevation to use a distance-based
    /// curve, and overrides firing to create an ArtilleryProjectile with parabolic flight, accuracy
    /// roll, and splash damage instead of a pooled bullet.
    /// </summary>
    public class ArtilleryWeapon : UnitCombat
    {
        [Header("Artillery Aiming")]
        [SerializeField] private float _minElevationAngle = 10f;
        [SerializeField] private float _maxElevationAngle = 52f;
        [SerializeField, Range(0f, 1f)] private float _minElevationDistanceRatio = 0.08f;
        [SerializeField, Min(0.1f)] private float _elevationCurvePower = 2.1f;

        [Header("Artillery Accuracy")]
        [SerializeField] private float _closeStationaryHitChance = 0.95f;
        [SerializeField] private float _maxRangeStationaryHitChance = 0.5f;
        [SerializeField] private float _closeMovingHitChance = 0.55f;
        [SerializeField] private float _maxRangeMovingHitChance = 0.1f;
        [SerializeField] private float _movingSpeedThreshold = 0.2f;
        [SerializeField] private float _maxMissRadius = 8f;

        [Header("Artillery Explosion")]
        [SerializeField] private float _splashRadius = 4.2f;
        [SerializeField] private float _directDamageMinMultiplier = 0.8f;
        [SerializeField] private float _directDamageMaxMultiplier = 1f;
        [SerializeField] private float _splashDamageMinMultiplier = 0.2f;
        [SerializeField] private float _splashDamageMaxMultiplier = 0.5f;

        [Header("Artillery Projectile")]
        [SerializeField] private float _minFlightTime = 0.85f;
        [SerializeField] private float _maxFlightTime = 2.25f;
        [SerializeField] private float _minArcHeight = 5f;
        [SerializeField] private float _maxArcHeight = 24f;

        protected override void Awake()
        {
            if (_turret != null)
                _turret.localRotation = Quaternion.identity;

            if (_gun != null)
                _gun.localRotation = Quaternion.identity;

            base.Awake();
        }

        /// <summary>
        /// Overrides the base direct-pitch logic. Maps distance ratio through a power curve to derive
        /// an elevation angle, then calls MoveGunPitch to step toward it.
        /// </summary>
        protected override bool RotateGunToTarget(Transform target)
        {
            Vector3 targetPoint = GetTargetPoint(target);
            float distanceRatio = GetDistanceRatio(targetPoint);
            float elevationRatio = Mathf.InverseLerp(_minElevationDistanceRatio, 1f, distanceRatio);
            elevationRatio = Mathf.Pow(elevationRatio, _elevationCurvePower);
            float targetElevation = Mathf.Lerp(_minElevationAngle, _maxElevationAngle, elevationRatio);
            float targetPitch = Mathf.Clamp(-targetElevation, _unitData.MinGunPitch, _unitData.MaxGunPitch);

            return MoveGunPitch(targetPitch);
        }

        /// <summary>
        /// Rolls for hit vs. miss, optionally offsets the impact point, snaps it to ground,
        /// then creates an ArtilleryProjectile with all required damage and flight parameters.
        /// </summary>
        protected override IEnumerator FireAtTarget(Transform target)
        {
            if (_unitData == null || _pointPosition == null || target == null)
                yield break;

            Vector3 start = _pointPosition.position;
            Vector3 targetPoint = GetTargetPoint(target);
            float distanceRatio = GetDistanceRatio(targetPoint);
            bool isMoving = IsMoving(_movingSpeedThreshold);
            float hitChance = GetHitChance(distanceRatio, isMoving);
            bool isDirectHit = Random.value <= hitChance;
            Vector3 impactPoint = isDirectHit
                ? targetPoint
                : GetMissPoint(targetPoint, distanceRatio, isMoving);

            impactPoint = SnapToGround(impactPoint);

            _shotEffects?.PlayShotEffect();

            float flightTime = Mathf.Lerp(_minFlightTime, _maxFlightTime, distanceRatio);
            float arcHeight = Mathf.Lerp(_minArcHeight, _maxArcHeight, distanceRatio);

            ArtilleryProjectile projectile = ArtilleryProjectile.Create(start);
            projectile.Initialize(
                start,
                impactPoint,
                arcHeight,
                flightTime,
                gameObject,
                _teamComponent,
                target,
                isDirectHit,
                _unitData.Damage,
                _splashRadius,
                _directDamageMinMultiplier,
                _directDamageMaxMultiplier,
                _splashDamageMinMultiplier,
                _splashDamageMaxMultiplier);
        }

        /// <summary>
        /// Lerps between stationary and moving hit-chance extremes based on distance ratio and motion state.
        /// </summary>
        private float GetHitChance(float distanceRatio, bool isMoving)
        {
            if (isMoving)
                return Mathf.Lerp(_closeMovingHitChance, _maxRangeMovingHitChance, distanceRatio);

            return Mathf.Lerp(_closeStationaryHitChance, _maxRangeStationaryHitChance, distanceRatio);
        }

        /// <summary>
        /// Generates a random offset point around the target within a miss radius scaled by distance and motion.
        /// </summary>
        private Vector3 GetMissPoint(Vector3 targetPoint, float distanceRatio, bool isMoving)
        {
            float missRadius = Mathf.Lerp(_splashRadius * 0.65f, _maxMissRadius, distanceRatio);

            if (isMoving)
                missRadius *= 1.35f;

            Vector2 randomDirection = Random.insideUnitCircle;

            if (randomDirection.sqrMagnitude < 0.01f)
                randomDirection = Vector2.right;

            randomDirection.Normalize();

            float randomDistance = Random.Range(_splashRadius * 0.55f, missRadius);

            return targetPoint + new Vector3(
                randomDirection.x * randomDistance,
                0f,
                randomDirection.y * randomDistance);
        }
    }
}
