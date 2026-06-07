using System;
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
    /// Базовий клас для всієї бойової поведінки юнітів. Обробляє виявлення цілей, наведення вежі/гармати,
    /// рух до дистанції атаки та стрільбу через BulletPool. Успадковується артилерією та автогарматними юнітами.
    /// </summary>
    public class UnitCombat : MonoBehaviour, IDamageable, ISimulationTickable
    {
        public event Action<UnitCombat> HealthChanged;

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
        private float _nextIdleScanTime;
        private float _idleScanYaw;
        private bool _hasIdleScanYaw;

        public TeamType Team => _teamComponent != null ? _teamComponent.Team : LocalPlayerContext.LocalTeam;
        public float AttackRange => _unitData != null ? _unitData.AttackRange : 0f;
        public UnitData UnitData => _unitData;
        public float MaxHealth => _unitData != null ? _unitData.MaxHealth : 0f;
        public float CurrentHealth => _health != null ? _health.CurrentHealth : MaxHealth;
        public float NormalizedHealth => MaxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / MaxHealth);
        public bool IsDead => _health != null && _health.IsDead;

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

            _lastTargetTime = Time.time;
            _nextIdleScanTime = Time.time;
            SetupTargetMask();
        }

        protected virtual void Update()
        {
            if (IsTargetValid(_aimTarget))
            {
                _hasIdleScanYaw = false;
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
            GameTickRunner.Register(this, 0.25f);
            SetupTargetMask();
        }

        protected virtual void OnDisable()
        {
            if (_teamComponent != null)
                _teamComponent.TeamChanged -= OnTeamChanged;

            EventManager.OnUnitMoveCommand -= OnMoveCommand;
            GameTickRunner.Unregister(this);
            StopAttack();
        }

        public void Tick(GameTickContext context)
        {
            CheckEnemies();
        }

        /// <summary>
        /// Призначає вручну обрану ціль атаки та сповіщає інші системи через EventManager.
        /// </summary>
        public void SetManualAttackTarget(Transform target)
        {
            _manualAttackTarget = target;

            if (IsTargetValid(target))
            {
                _aimTarget = target;
                _lastTargetTime = Time.time;
                _hasIdleScanYaw = false;
            }

            EventManager.RaiseUnitAttackTargetChanged(gameObject, target);
        }

        /// <summary>
        /// Отримує вхідну шкоду через інтерфейс IDamageable; знищує юніт, коли здоров'я досягає нуля.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_health == null || _health.IsDead || damage <= 0f)
                return;

            float previousHealth = _health.CurrentHealth;
            _health.TakeDamage(damage);

            if (!Mathf.Approximately(previousHealth, _health.CurrentHealth))
                HealthChanged?.Invoke(this);

            if (_health.IsDead)
                Die();
        }

        /// <summary>
        /// Викидає подію скасування вибору та знищує цей GameObject, коли здоров'я вичерпано.
        /// </summary>
        private void Die()
        {
            EventManager.RaiseUnitDestroyed(gameObject);
            EventManager.RaiseUnitDeselected(gameObject);
            Destroy(gameObject);
        }

        /// <summary>
        /// Обробляє трансляцію команди переміщення: очищає стан атаки та записує пункт призначення,
        /// щоб CheckEnemies міг все одно атакувати цілі, що трапляються на шляху.
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
        /// Виконується кожні 0.25 с. Обирає найкращу ціль (ручну або автоматичну), веде NavMeshAgent
        /// до дистанції атаки за потреби, та запускає або зупиняє корутину атаки відповідно.
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

            float distance = Vector3.Distance(transform.position, target.position);

            if (hasManualTarget && distance > _unitData.AttackRange)
            {
                MoveToAttackRange(target);

                Transform opportunisticTarget = _unitData.OpportunisticTargeting
                    ? FindAutoTarget()
                    : null;

                if (IsTargetValid(opportunisticTarget))
                {
                    _aimTarget = opportunisticTarget;
                    _lastTargetTime = Time.time;

                    if (AimAtTarget(opportunisticTarget))
                        StartAttackIfNeeded(opportunisticTarget);
                    else
                        StopAttack();

                    return;
                }

                StopAttack();
                _aimTarget = target;
                _lastTargetTime = Time.time;
                return;
            }

            _aimTarget = target;
            _lastTargetTime = Time.time;

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
        /// Розпочинає корутину Attack для заданої цілі, спочатку зупиняючи попередню.
        /// Нічого не робить, якщо та сама ціль вже атакується.
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
        /// Повертає true, коли юніт знаходиться в межах дистанції зупинки від заданого гравцем пункту призначення.
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
        /// Використовує Physics.OverlapSphere для пошуку найближчого ворога в межах AttackRange за правильною маскою шару.
        /// </summary>
        private Transform FindAutoTarget()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                _unitData.AttackRange,
                _targetMask,
                QueryTriggerInteraction.Collide);

            Transform closestTarget = null;
            float closestDistanceSqr = float.MaxValue;

            foreach (Collider hit in hits)
            {
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
        /// Дає команду NavMeshAgent переміститися до дійсної позиції на NavMesh трохи в межах дистанції атаки від цілі.
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
        /// Прив'язує запитувану позицію атаки до NavMesh і перевіряє наявність повного або часткового
        /// шляху; встановлює resolvedPosition на найближчу досяжну точку.
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
        /// Корутина, що повторно викликає FireAtTarget з паузами AttackDelay до тих пір, поки ціль дійсна.
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
        /// Зупиняє запущену корутину атаки та очищає посилання на поточну ціль.
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
        /// Будує маску шару, що використовується FindAutoTarget, на основі поточної команди юніта.
        /// </summary>
        private void SetupTargetMask()
        {
            _targetMask = LayerMask.GetMask("PlayerUnit", "EnemyUnit", "Building");
        }

        /// <summary>
        /// Перебудовує маску цілей та очищає поточні цілі щоразу, коли змінюється команда юніта.
        /// </summary>
        private void OnTeamChanged(TeamType team)
        {
            SetupTargetMask();
            _manualAttackTarget = null;
            _aimTarget = null;
            StopAttack();
        }

        /// <summary>
        /// Отримує кулю з BulletPool, розміщує її на дульному зрізі та ініціалізує її
        /// BulletController. Перевизначається в підкласах для іншої поведінки стрільби (наприклад, автогармата).
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
        /// Повертає вежу (по осі Y) та гармату (кут підйому по X) до цілі щокадру.
        /// Повертає true лише тоді, коли обидві осі знаходяться в межах AimAngleTolerance.
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
        /// Поступово обертає трансформ вежі локально по осі Y до цілі зі швидкістю TurretRotationSpeed.
        /// Повертає true, коли відхилення по горизонту знаходиться в межах AimAngleTolerance.
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
        /// Обчислює необхідний кут підйому гармати для наведення на ціль і делегує виконання до MoveGunPitch.
        /// Перевизначається в ArtilleryWeapon для використання кривої підйому замість прямого кута.
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
        /// Покроково обертає локальний кут X гармати до targetPitch зі швидкістю GunPitchSpeed.
        /// Повертає true, коли відхилення знаходиться в межах AimAngleTolerance.
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
        /// Поступово повертає вежу вперед (0 по горизонту) та гармату в горизонтальне положення через ReturnTurretDelay
        /// секунд після втрати цілі.
        /// </summary>
        protected virtual void HandleIdleTurret()
        {
            if (_unitData == null || _turret == null || _gun == null)
                return;

            if (Time.time < _lastTargetTime + _unitData.ReturnTurretDelay)
                return;

            if (Time.time >= _lastTargetTime + _unitData.IdleScanDelay)
            {
                if (!_hasIdleScanYaw || Time.time >= _nextIdleScanTime)
                {
                    _idleScanYaw = UnityEngine.Random.Range(-_unitData.IdleScanYawRange, _unitData.IdleScanYawRange);
                    _nextIdleScanTime = Time.time + UnityEngine.Random.Range(
                        _unitData.IdleScanIntervalMin,
                        _unitData.IdleScanIntervalMax);
                    _hasIdleScanYaw = true;
                }

                float scanYaw = Mathf.MoveTowardsAngle(
                    _turret.localEulerAngles.y,
                    _idleScanYaw,
                    _unitData.IdleTurretRotationSpeed * Time.deltaTime);

                _turret.localRotation = Quaternion.Euler(0f, scanYaw, 0f);
                MoveGunPitch(0f);
                return;
            }

            _hasIdleScanYaw = false;

            float currentYaw = _turret.localEulerAngles.y;
            float newYaw = Mathf.MoveTowardsAngle(
                currentYaw,
                0f,
                _unitData.IdleTurretRotationSpeed * Time.deltaTime);

            _turret.localRotation = Quaternion.Euler(0f, newYaw, 0f);

            MoveGunPitch(0f);
        }

        /// <summary>
        /// Повертає true, коли ціль не є null і її GameObject активний в ієрархії.
        /// </summary>
        protected bool IsTargetValid(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;

            if (target.GetComponentInParent<IDamageable>() == null)
                return false;

            ITeam targetTeam = target.GetComponentInParent<ITeam>();
            return targetTeam != null && TeamRelations.AreHostile(Team, targetTeam.Team);
        }

        /// <summary>
        /// Повертає центр меж колайдера цілі (кешований для кожної цілі) або позицію її трансформу
        /// як запасний варіант, для точного наведення на тіло юніта.
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
        /// Повертає відстань XZ до targetPoint як частку [0, 1] від AttackRange.
        /// Використовується артилерією для змішування кута підйому та ймовірності влучання залежно від дистанції.
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
        /// Повертає true, коли поточна швидкість NavMeshAgent досягає або перевищує speedThreshold.
        /// Використовується артилерією для застосування штрафу точності при стрільбі по рухомій цілі.
        /// </summary>
        protected bool IsMoving(float speedThreshold)
        {
            if (_agent == null || !_agent.enabled)
                return false;

            return _agent.velocity.magnitude >= speedThreshold;
        }

        /// <summary>
        /// Пускає промінь вниз з 30 одиниць над заданою точкою для прив'язки до геометрії терену/землі.
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
