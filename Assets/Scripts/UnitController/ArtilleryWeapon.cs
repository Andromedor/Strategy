using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace UnitController
{
    public class ArtilleryWeapon : MonoBehaviour
    {
        [Header("Aiming")]
        [SerializeField] private float _minElevationAngle = 8f;
        [SerializeField] private float _maxElevationAngle = 70f;

        [Header("Accuracy")]
        [SerializeField] private float _closeStationaryHitChance = 0.95f;
        [SerializeField] private float _maxRangeStationaryHitChance = 0.5f;
        [SerializeField] private float _closeMovingHitChance = 0.55f;
        [SerializeField] private float _maxRangeMovingHitChance = 0.1f;
        [SerializeField] private float _movingSpeedThreshold = 0.2f;
        [SerializeField] private float _maxMissRadius = 8f;

        [Header("Explosion")]
        [SerializeField] private float _splashRadius = 4.2f;
        [SerializeField] private float _directDamageMinMultiplier = 0.8f;
        [SerializeField] private float _directDamageMaxMultiplier = 1f;
        [SerializeField] private float _splashDamageMinMultiplier = 0.2f;
        [SerializeField] private float _splashDamageMaxMultiplier = 0.5f;

        [Header("Projectile")]
        [SerializeField] private float _minFlightTime = 0.85f;
        [SerializeField] private float _maxFlightTime = 2.25f;
        [SerializeField] private float _minArcHeight = 5f;
        [SerializeField] private float _maxArcHeight = 24f;

        private UnitData _unitData;
        private Transform _turret;
        private Transform _gun;
        private Transform _muzzle;
        private TankCannonEffects _shotEffects;
        private NavMeshAgent _agent;
        private TeamComponent _teamComponent;

        public void Configure(
            UnitData unitData,
            Transform turret,
            Transform gun,
            Transform muzzle,
            TankCannonEffects shotEffects,
            NavMeshAgent agent,
            TeamComponent teamComponent)
        {
            _unitData = unitData;
            _turret = turret;
            _gun = gun;
            _muzzle = muzzle;
            _shotEffects = shotEffects;
            _agent = agent;
            _teamComponent = teamComponent;
        }

        public bool AimAtTarget(Transform target)
        {
            if (_unitData == null || _turret == null || _gun == null || target == null)
                return false;

            bool turretReady = RotateTurretToTarget(target);
            bool gunReady = ElevateGunForDistance(target);

            return turretReady && gunReady;
        }

        public IEnumerator Fire(Transform target, GameObject owner)
        {
            if (_unitData == null || _muzzle == null || target == null)
                yield break;

            Vector3 start = _muzzle.position;
            Vector3 targetPoint = GetTargetPoint(target);
            float distanceRatio = GetDistanceRatio(targetPoint);
            bool isMoving = IsMoving();
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
                owner,
                _teamComponent,
                target,
                isDirectHit,
                _unitData.Damage,
                _splashRadius,
                _directDamageMinMultiplier,
                _directDamageMaxMultiplier,
                _splashDamageMinMultiplier,
                _splashDamageMaxMultiplier);

            yield break;
        }

        private bool RotateTurretToTarget(Transform target)
        {
            Vector3 worldDirection = target.position - _turret.position;
            Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
            localDirection.y = 0f;

            if (localDirection.sqrMagnitude < 0.01f)
                return true;

            float targetYaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float currentYaw = _turret.localEulerAngles.y;
            float newYaw = Mathf.MoveTowardsAngle(
                currentYaw,
                targetYaw,
                _unitData.TurretRotationSpeed * Time.deltaTime);

            _turret.localRotation = Quaternion.Euler(0f, newYaw, 0f);

            return Mathf.Abs(Mathf.DeltaAngle(newYaw, targetYaw)) <= _unitData.AimAngleTolerance;
        }

        private bool ElevateGunForDistance(Transform target)
        {
            Vector3 targetPoint = GetTargetPoint(target);
            float distanceRatio = GetDistanceRatio(targetPoint);
            float targetElevation = Mathf.Lerp(_minElevationAngle, _maxElevationAngle, distanceRatio);
            float targetPitch = Mathf.Clamp(-targetElevation, _unitData.MinGunPitch, _unitData.MaxGunPitch);
            float currentPitch = _gun.localEulerAngles.x;
            float newPitch = Mathf.MoveTowardsAngle(
                currentPitch,
                targetPitch,
                _unitData.GunPitchSpeed * Time.deltaTime);

            _gun.localRotation = Quaternion.Euler(newPitch, 0f, 0f);

            return Mathf.Abs(Mathf.DeltaAngle(newPitch, targetPitch)) <= _unitData.AimAngleTolerance;
        }

        private Vector3 GetTargetPoint(Transform target)
        {
            Collider targetCollider = target.GetComponentInParent<Collider>();

            if (targetCollider != null)
                return targetCollider.bounds.center;

            return target.position;
        }

        private float GetDistanceRatio(Vector3 targetPoint)
        {
            Vector2 from = new Vector2(transform.position.x, transform.position.z);
            Vector2 to = new Vector2(targetPoint.x, targetPoint.z);
            float distance = Vector2.Distance(from, to);

            if (_unitData.AttackRange <= 0f)
                return 1f;

            return Mathf.Clamp01(distance / _unitData.AttackRange);
        }

        private float GetHitChance(float distanceRatio, bool isMoving)
        {
            if (isMoving)
                return Mathf.Lerp(_closeMovingHitChance, _maxRangeMovingHitChance, distanceRatio);

            return Mathf.Lerp(_closeStationaryHitChance, _maxRangeStationaryHitChance, distanceRatio);
        }

        private bool IsMoving()
        {
            if (_agent == null || !_agent.enabled)
                return false;

            return _agent.velocity.magnitude >= _movingSpeedThreshold;
        }

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

        private Vector3 SnapToGround(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * 30f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;

            return point;
        }
    }
}
