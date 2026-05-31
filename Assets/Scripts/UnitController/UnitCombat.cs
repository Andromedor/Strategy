using System.Collections;
using Strategy.Buildings;
using Strategy.Units;
using UnityEngine;
using UnityEngine.AI;

using Strategy.Core;
using Strategy.Data;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Base class for all unit combat behaviour. Handles target detection, turret/gun aiming,
    /// movement to attack range, and firing via BulletPool. Subclassed by artillery and autocannon units.
    /// </summary>
    public class UnitCombat : MonoBehaviour, IDamageable
    {
        [Header("Data")]
        [SerializeField] protected UnitData _unitData;

        [Header("References")]
        [SerializeField] protected Transform _pointPosition;
        [SerializeField] protected TankCannonEffects _shotEffects;

        [Header("Aiming")]
        [SerializeField] protected Transform _turret;
        [SerializeField] protected Transform _gun;

        protected NavMeshAgent _agent;
        protected TeamComponent _teamComponent;

        private Coroutine _attackCoroutine;
        private Transform _manualAttackTarget;
        private Transform _currentAttackTarget;
        private Transform _aimTarget;
        private Transform _cachedColliderTarget;
        private Collider _cachedTargetCollider;
        private UnitHealth _health;
        private LayerMask _targetMask;
        private const float AttackPositionSampleRadius = 4f;
        private bool _hasPlayerMoveCommand;
        private Vector3 _playerMoveDestination;
        protected float _lastTargetTime;

        public TeamType Team => _teamComponent != null ? _teamComponent.Team : TeamType.Player;
        public float AttackRange => _unitData != null ? _unitData.AttackRange : 0f;
        public UnitData UnitData => _unitData;
        public float MaxHealth => _unitData != null ? _unitData.MaxHealth : 0f;
        public float CurrentHealth => _health != null ? _health.CurrentHealth : MaxHealth;

        protected virtual void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _teamComponent = GetComponent<TeamComponent>();

            if (_unitData != null)
                _health = new UnitHealth(_unitData.MaxHealth);

            if (_shotEffects == null)
                _shotEffects = GetComponent<TankCannonEffects>();

            if (_shotEffects != null)
                _shotEffects.Configure(_gun, _pointPosition);

            SetupTargetMask();
        }

        protected virtual void Update()
        {
            if (IsTargetValid(_aimTarget))
            {
                AimAtTarget(_aimTarget);
            }
            else
            {
                HandleIdleTurret();
            }
        }

        protected virtual void OnEnable()
        {
            if (_teamComponent != null)
                _teamComponent.TeamChanged += OnTeamChanged;

            EventManager.OnUnitMoveCommand += OnMoveCommand;
            SetupTargetMask();
        }

        protected virtual void OnDisable()
        {
            if (_teamComponent != null)
                _teamComponent.TeamChanged -= OnTeamChanged;

            EventManager.OnUnitMoveCommand -= OnMoveCommand;
            StopAttack();
        }

        protected virtual void Start()
        {
            InvokeRepeating(nameof(CheckEnemies), 0f, 0.25f);
        }

        /// <summary>
        /// Assigns a manually chosen attack target and notifies other systems via EventManager.
        /// </summary>
        public void SetManualAttackTarget(Transform target)
        {
            _manualAttackTarget = target;
            EventManager.RaiseUnitAttackTargetChanged(gameObject, target);
        }

        /// <summary>
        /// Receives incoming damage via the IDamageable interface; destroys the unit when health reaches zero.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_health == null)
                return;

            _health.TakeDamage(damage);

            if (_health.IsDead)
                Die();
        }

        /// <summary>
        /// Fires the deselection event and destroys this GameObject when health is depleted.
        /// </summary>
        private void Die()
        {
            EventManager.RaiseUnitDeselected(gameObject);
            Destroy(gameObject);
        }

        /// <summary>
        /// Handles a move command broadcast: clears attack state and records the destination
        /// so CheckEnemies can still engage targets encountered along the way.
        /// </summary>
        private void OnMoveCommand(GameObject unit, Vector3 destination)
        {
            if (unit != gameObject)
                return;

            _manualAttackTarget = null;
            _aimTarget = null;
            StopAttack();

            _hasPlayerMoveCommand = true;
            _playerMoveDestination = destination;
        }

        /// <summary>
        /// Runs every 0.25 s. Selects the best target (manual or auto), drives the NavMeshAgent
        /// toward attack range if needed, and starts or stops the attack coroutine accordingly.
        /// </summary>
        private void CheckEnemies()
        {
            if (_unitData == null)
                return;

            if (_hasPlayerMoveCommand && HasReachedPlayerMoveDestination())
                _hasPlayerMoveCommand = false;

            bool hasManualTarget = IsTargetValid(_manualAttackTarget);
            Transform target = hasManualTarget ? _manualAttackTarget : FindAutoTarget();

            if (!IsTargetValid(target))
            {
                StopAttack();
                _aimTarget = null;
                _manualAttackTarget = null;
                return;
            }

            _aimTarget = target;
            _lastTargetTime = Time.time;

            float distance = Vector3.Distance(transform.position, target.position);

            if (_hasPlayerMoveCommand)
            {
                if (distance <= _unitData.AttackRange && AimAtTarget(target))
                    StartAttackIfNeeded(target);
                else
                {
                    StopAttack();

                    if (distance > _unitData.AttackRange)
                        _aimTarget = null;
                }

                return;
            }

            if (distance > _unitData.AttackRange)
            {
                StopAttack();
                _aimTarget = null;

                if (hasManualTarget)
                    MoveToAttackRange(target);

                return;
            }

            if (_agent != null && _agent.enabled)
                _agent.ResetPath();

            if (!AimAtTarget(target))
            {
                StopAttack();
                return;
            }

            StartAttackIfNeeded(target);
        }

        /// <summary>
        /// Begins an Attack coroutine for the given target, stopping any previous one first.
        /// Does nothing if the same target is already being engaged.
        /// </summary>
        private void StartAttackIfNeeded(Transform target)
        {
            EventManager.RaiseUnitAttackTargetChanged(gameObject, target);

            if (_attackCoroutine != null && _currentAttackTarget == target)
                return;

            StopAttack();

            _currentAttackTarget = target;
            _attackCoroutine = StartCoroutine(Attack(target));
        }

        /// <summary>
        /// Returns true once the unit is within stopping distance of the player-issued move destination.
        /// </summary>
        private bool HasReachedPlayerMoveDestination()
        {
            if (_agent == null || !_agent.enabled)
                return true;

            Vector3 destinationOffset = _playerMoveDestination - transform.position;
            destinationOffset.y = 0f;

            if (destinationOffset.magnitude <= _agent.stoppingDistance + 0.35f)
                return true;

            if (!_agent.hasPath && !_agent.pathPending)
                return false;

            return !_agent.pathPending &&
                   _agent.remainingDistance <= _agent.stoppingDistance + 0.2f;
        }

        /// <summary>
        /// Uses Physics.OverlapSphere to find the closest enemy within AttackRange on the correct layer mask.
        /// </summary>
        private Transform FindAutoTarget()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                _unitData.AttackRange,
                _targetMask);

            Transform closestTarget = null;
            float closestDistanceSqr = float.MaxValue;

            foreach (Collider hit in hits)
            {
                ITeam targetTeam = hit.GetComponentInParent<ITeam>();

                if (targetTeam == null || targetTeam.Team == Team)
                    continue;

                Transform target = hit.transform;

                if (!IsTargetValid(target))
                    continue;

                float distanceSqr = (target.position - transform.position).sqrMagnitude;

                if (distanceSqr >= closestDistanceSqr)
                    continue;

                closestDistanceSqr = distanceSqr;
                closestTarget = target;
            }

            return closestTarget;
        }

        /// <summary>
        /// Commands the NavMeshAgent to move to a NavMesh-valid position just inside attack range of the target.
        /// </summary>
        private void MoveToAttackRange(Transform target)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            Vector3 directionFromTarget = (transform.position - target.position).normalized;

            if (directionFromTarget.sqrMagnitude < 0.01f)
                directionFromTarget = -transform.forward;

            Vector3 attackPosition =
                target.position + directionFromTarget * (_unitData.AttackRange * 0.85f);

            if (!TryResolveAttackDestination(attackPosition, out attackPosition))
                return;

            _agent.SetDestination(attackPosition);
        }

        /// <summary>
        /// Snaps the requested attack position onto the NavMesh and validates that a full or partial
        /// path exists; sets resolvedPosition to the best reachable point.
        /// </summary>
        private bool TryResolveAttackDestination(Vector3 requestedPosition, out Vector3 resolvedPosition)
        {
            resolvedPosition = requestedPosition;

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return false;

            if (NavMesh.SamplePosition(
                    requestedPosition,
                    out NavMeshHit navHit,
                    AttackPositionSampleRadius,
                    _agent.areaMask))
            {
                resolvedPosition = navHit.position;
            }

            NavMeshPath path = new NavMeshPath();

            if (!_agent.CalculatePath(resolvedPosition, path))
                return false;

            if (path.status == NavMeshPathStatus.PathComplete)
                return true;

            if (path.status != NavMeshPathStatus.PathPartial ||
                path.corners == null ||
                path.corners.Length < 2)
                return false;

            Vector3 reachablePoint = path.corners[path.corners.Length - 1];
            float minMoveDistance = Mathf.Max(0.25f, _agent.stoppingDistance + 0.1f);

            if ((reachablePoint - transform.position).sqrMagnitude <= minMoveDistance * minMoveDistance)
                return false;

            resolvedPosition = reachablePoint;
            return true;
        }

        /// <summary>
        /// Coroutine that calls FireAtTarget repeatedly with AttackDelay pauses until the target is invalid.
        /// </summary>
        private IEnumerator Attack(Transform target)
        {
            while (IsTargetValid(target))
            {
                yield return FireAtTarget(target);
                yield return new WaitForSeconds(_unitData.AttackDelay);
            }

            if (_manualAttackTarget == target)
                _manualAttackTarget = null;

            if (_currentAttackTarget == target)
                _currentAttackTarget = null;

            _attackCoroutine = null;
        }

        /// <summary>
        /// Stops the running attack coroutine and clears the current target reference.
        /// </summary>
        private void StopAttack()
        {
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }

            _currentAttackTarget = null;
        }

        /// <summary>
        /// Builds the layer mask used by FindAutoTarget based on the unit's current team.
        /// </summary>
        private void SetupTargetMask()
        {
            _targetMask = Team == TeamType.Player
                ? LayerMask.GetMask("EnemyUnit")
                : LayerMask.GetMask("PlayerUnit");
        }

        /// <summary>
        /// Rebuilds the target mask and clears current targets whenever the unit's team changes.
        /// </summary>
        private void OnTeamChanged(TeamType team)
        {
            SetupTargetMask();
            _manualAttackTarget = null;
            _aimTarget = null;
            StopAttack();
        }

        /// <summary>
        /// Retrieves a bullet from BulletPool, positions it at the muzzle, and initialises its
        /// BulletController. Override in subclasses for different firing behaviour (e.g. autocannon).
        /// </summary>
        protected virtual IEnumerator FireAtTarget(Transform target)
        {
            if (_unitData == null || _pointPosition == null || target == null)
                yield break;

            if (BulletPool.Instance == null || !BulletPool.Instance.TryGetBullet(out GameObject bullet))
                yield break;

            bullet.transform.position = _pointPosition.position;
            bullet.transform.rotation = _pointPosition.rotation;

            BulletController bulletController = bullet.GetComponent<BulletController>();

            if (bulletController == null)
            {
                BulletPool.Instance.ReturnBullet(bullet);
                yield break;
            }

            bulletController.Initialize(_unitData.Damage, _unitData.Speed, target, gameObject);
            _shotEffects?.PlayShotEffect();
        }

        /// <summary>
        /// Rotates turret (Y) and gun (X pitch) toward the target each frame.
        /// Returns true only when both axes are within AimAngleTolerance.
        /// </summary>
        protected virtual bool AimAtTarget(Transform target)
        {
            if (target == null || _turret == null || _gun == null || _unitData == null)
                return false;

            bool turretReady = RotateTurretToTarget(target);
            bool gunReady = RotateGunToTarget(target);

            return turretReady && gunReady;
        }

        /// <summary>
        /// Incrementally rotates the turret transform in local Y toward the target at TurretRotationSpeed.
        /// Returns true when the yaw error is within AimAngleTolerance.
        /// </summary>
        protected virtual bool RotateTurretToTarget(Transform target)
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

        /// <summary>
        /// Computes the required gun pitch to aim at the target and delegates to MoveGunPitch.
        /// Override in ArtilleryWeapon to use an elevation curve instead of direct pitch.
        /// </summary>
        protected virtual bool RotateGunToTarget(Transform target)
        {
            Vector3 worldDirection = target.position - _gun.position;
            Vector3 localDirection = _turret.InverseTransformDirection(worldDirection);
            float horizontalDistance = new Vector2(localDirection.x, localDirection.z).magnitude;
            float targetPitch = -Mathf.Atan2(localDirection.y, horizontalDistance) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch, _unitData.MinGunPitch, _unitData.MaxGunPitch);

            return MoveGunPitch(targetPitch);
        }

        /// <summary>
        /// Steps the gun's local X rotation toward targetPitch at GunPitchSpeed.
        /// Returns true when within AimAngleTolerance.
        /// </summary>
        protected bool MoveGunPitch(float targetPitch)
        {
            float currentPitch = _gun.localEulerAngles.x;
            float newPitch = Mathf.MoveTowardsAngle(
                currentPitch,
                targetPitch,
                _unitData.GunPitchSpeed * Time.deltaTime);

            _gun.localRotation = Quaternion.Euler(newPitch, 0f, 0f);

            return Mathf.Abs(Mathf.DeltaAngle(newPitch, targetPitch)) <= _unitData.AimAngleTolerance;
        }

        /// <summary>
        /// Slowly returns the turret to forward (0 yaw) and gun to level pitch after ReturnTurretDelay
        /// seconds without a target.
        /// </summary>
        protected virtual void HandleIdleTurret()
        {
            if (_unitData == null || _turret == null || _gun == null)
                return;

            if (Time.time < _lastTargetTime + _unitData.ReturnTurretDelay)
                return;

            float currentYaw = _turret.localEulerAngles.y;
            float newYaw = Mathf.MoveTowardsAngle(
                currentYaw,
                0f,
                _unitData.IdleTurretRotationSpeed * Time.deltaTime);

            _turret.localRotation = Quaternion.Euler(0f, newYaw, 0f);

            MoveGunPitch(0f);
        }

        /// <summary>
        /// Returns true when target is non-null and its GameObject is active in the hierarchy.
        /// </summary>
        protected bool IsTargetValid(Transform target)
        {
            return target != null && target.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Returns the collider bounds center of the target (cached per target) or its transform position
        /// as a fallback, for accurate aiming at the body of the unit.
        /// </summary>
        protected Vector3 GetTargetPoint(Transform target)
        {
            if (target != _cachedColliderTarget)
            {
                _cachedColliderTarget = target;
                _cachedTargetCollider = target.GetComponentInParent<Collider>();
            }

            if (_cachedTargetCollider != null)
                return _cachedTargetCollider.bounds.center;

            return target.position;
        }

        /// <summary>
        /// Returns the XZ distance to targetPoint as a [0, 1] fraction of AttackRange.
        /// Used by artillery to blend elevation angle and hit-chance by range.
        /// </summary>
        protected float GetDistanceRatio(Vector3 targetPoint)
        {
            Vector2 from = new Vector2(transform.position.x, transform.position.z);
            Vector2 to = new Vector2(targetPoint.x, targetPoint.z);
            float distance = Vector2.Distance(from, to);

            if (_unitData == null || _unitData.AttackRange <= 0f)
                return 1f;

            return Mathf.Clamp01(distance / _unitData.AttackRange);
        }

        /// <summary>
        /// Returns true when the NavMeshAgent's current speed meets or exceeds speedThreshold.
        /// Used by artillery to apply a moving-target accuracy penalty.
        /// </summary>
        protected bool IsMoving(float speedThreshold)
        {
            if (_agent == null || !_agent.enabled)
                return false;

            return _agent.velocity.magnitude >= speedThreshold;
        }

        /// <summary>
        /// Raycasts downward from 30 units above the given point to snap it to terrain/ground geometry.
        /// </summary>
        protected Vector3 SnapToGround(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * 30f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;

            return point;
        }
    }
}
