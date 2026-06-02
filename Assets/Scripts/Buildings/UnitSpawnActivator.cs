using System;
using System.Collections;
using Strategy.Units;
using UnityEngine;
using UnityEngine.AI;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Керує переходом щойно створеного юніта зі стану вимкненого заводу до повноцінного ігрового режиму.
    /// Вручну переміщує юніта до точки виходу, після чого прив'язує його до NavMesh та вмикає всі ігрові компоненти.
    /// </summary>
    public class UnitSpawnActivator : MonoBehaviour
    {
        [SerializeField] private float _exitMoveSpeed = 4f;
        // Швидкість ручного виїзду з заводу.

        [SerializeField] private float _exitDistance = 0.2f;
        // Наскільки близько треба під'їхати до ExitPoint.

        [SerializeField] private float _navMeshSnapRadius = 6f;
        [SerializeField] private float _navMeshFallbackSnapRadius = 14f;
        [SerializeField, Min(0.1f)] private float _trafficCorridorPadding = 0.85f;
        [SerializeField, Min(0.1f)] private float _trafficYieldWaitTimeout = 3f;

        private NavMeshAgent _agent;
        private UnitCombat _combat;
        private UnitSelectionState _selectionState;
        private NavMeshVehicleMotor _vehicleMotor;
        private Collider[] _colliders;
        private bool _isSpawning;
        private bool _hasQueuedMoveCommand;
        private Vector3 _queuedMoveDestination;
        private bool _hasDefaultMoveCommand;
        private Vector3 _defaultMoveDestination;
        private Action<Vector3> _releaseDefaultMoveReservation;
        private bool _hasActiveDefaultMoveReservation;
        private Vector3 _activeDefaultMoveDestination;
        private Coroutine _releaseDefaultMoveReservationCoroutine;
        private Coroutine _releaseMoveReservationCoroutine;

        public bool IsSpawning => _isSpawning;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _combat = GetComponent<UnitCombat>();
            _selectionState = GetComponent<UnitSelectionState>();
            _vehicleMotor = GetComponent<NavMeshVehicleMotor>();
            _colliders = GetComponentsInChildren<Collider>();
            UnitTrafficCoordinator.Ensure(gameObject);
        }

        private void OnEnable()
        {
            EventManager.OnUnitMoveCommand += OnUnitMoveCommand;
        }

        private void OnDisable()
        {
            EventManager.OnUnitMoveCommand -= OnUnitMoveCommand;
            ReleaseDefaultMoveReservation();
            ReleaseMoveReservation();
        }

        /// <summary>
        /// Перемикає юніта між станом виїзду із заводу (бойова логіка вимкнена, агент зупинений)
        /// та повністю активним станом (розміщено на NavMesh, ігровий процес увімкнено).
        /// </summary>
        public void SetSpawningState(bool isSpawning)
        {
            if (isSpawning)
            {
                _isSpawning = true;
                _hasQueuedMoveCommand = false;
                _hasDefaultMoveCommand = false;
                ReleaseDefaultMoveReservation();
                ReleaseMoveReservation();
                SetCombatEnabled(false);

                if (_selectionState != null)
                    _selectionState.enabled = true;

                if (_vehicleMotor != null)
                    _vehicleMotor.enabled = false;

                if (_agent != null)
                {
                    if (_agent.enabled && _agent.isOnNavMesh)
                        _agent.ResetPath();

                    _agent.enabled = false;
                }

                SetCollidersEnabled(true);
            }
            else
            {
                if (!EnableNavigationAtCurrentPosition())
                {
                    Debug.LogError(
                        $"{name} could not be activated after factory exit because no reachable NavMesh was found near {transform.position}.",
                        this);
                    return;
                }

                _isSpawning = false;

                if (_vehicleMotor != null)
                    _vehicleMotor.enabled = true;

                SetCollidersEnabled(true);
                SetCombatEnabled(true);

                if (_selectionState != null)
                    _selectionState.enabled = true;

                ApplyMoveAfterSpawn();
            }
        }

        /// <summary>
        /// Запам'ятовує останній наказ руху, виданий під час виїзду, і виконає його лише після активації NavMeshAgent.
        /// </summary>
        public void QueueMoveAfterSpawn(Vector3 destination)
        {
            _queuedMoveDestination = destination;
            _hasQueuedMoveCommand = true;
            // Агент ще може бути вимкнений під час виїзду, але ціль гравця вже треба забронювати, щоб наступний юніт не взяв ту саму точку.
            ReserveMoveDestination(destination);

            if (_hasDefaultMoveCommand || _releaseDefaultMoveReservation != null)
                ReleaseDefaultMoveReservation();
        }

        public void SetDefaultMoveAfterSpawn(Vector3 destination, Action<Vector3> releaseReservation = null)
        {
            if (_hasQueuedMoveCommand)
            {
                releaseReservation?.Invoke(destination);
                return;
            }

            _defaultMoveDestination = destination;
            _hasDefaultMoveCommand = true;
            _releaseDefaultMoveReservation = releaseReservation;
            // Rally-наказ поводиться як звичайна майбутня команда руху, поки гравець не переб'є її власним наказом.
            ReserveMoveDestination(destination);
        }

        /// <summary>Плавно переміщує юніта до exitPoint зі швидкістю _exitMoveSpeed, повертаючи його в напрямку руху, після чого викликає SetSpawningState(false).</summary>
        public IEnumerator MoveOutOfFactory(Vector3 exitPoint)
        {
            yield return MoveOutOfFactory(exitPoint, exitPoint);
        }

        /// <summary>
        /// Проводить юніта до точки очищення брами, після чого активує NavMeshAgent.
        /// Подальший рух отримує наказ гравця, а якщо його немає — стандартну точку роз'їзду заводу.
        /// </summary>
        public IEnumerator MoveOutOfFactory(Vector3 gatePoint, Vector3 defaultDestination)
        {
            yield return MoveOutOfFactory(gatePoint, defaultDestination, null);
        }

        public IEnumerator MoveOutOfFactory(
            Vector3 gatePoint,
            Vector3 defaultDestination,
            Action<Vector3> releaseDefaultReservation)
        {
            if (_hasQueuedMoveCommand)
                releaseDefaultReservation?.Invoke(defaultDestination);
            else
                SetDefaultMoveAfterSpawn(defaultDestination, releaseDefaultReservation);

            yield return MoveToFactoryPoint(gatePoint);
            SetSpawningState(false);
        }

        private IEnumerator MoveToFactoryPoint(Vector3 targetPoint)
        {
            float blockedSince = -1f;

            while (Vector3.Distance(transform.position, targetPoint) > _exitDistance)
            {
                if (ShouldWaitForTrafficYield(targetPoint, ref blockedSince))
                {
                    yield return null;
                    continue;
                }

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPoint,
                    _exitMoveSpeed * Time.deltaTime
                );

                Vector3 direction = targetPoint - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(direction);

                yield return null;
            }

            transform.position = targetPoint;
        }

        private bool ShouldWaitForTrafficYield(Vector3 targetPoint, ref float blockedSince)
        {
            float corridorRadius = ResolveExitTrafficCorridorRadius();
            // Поки NavMeshAgent вимкнений, юніт виїжджає вручну, тому просимо idle-союзників звільнити коридор перед MoveTowards.
            bool blocked = UnitTrafficCoordinator.RequestYieldForCorridor(
                gameObject,
                transform.position,
                targetPoint,
                corridorRadius);

            if (!blocked)
            {
                blockedSince = -1f;
                return false;
            }

            if (blockedSince < 0f)
                blockedSince = Time.time;

            // Таймаут не дає виробництву зависнути назавжди, якщо коридор неможливо звільнити через налаштування сцени.
            return Time.time - blockedSince <= _trafficYieldWaitTimeout;
        }

        private float ResolveExitTrafficCorridorRadius()
        {
            float agentRadius = _agent != null ? _agent.radius : 1f;
            return Mathf.Max(0.25f, agentRadius + _trafficCorridorPadding);
        }

        /// <summary>
        /// Вибирає точку на NavMesh поблизу поточної позиції юніта (із запасним радіусом) та переносить туди NavMeshAgent.
        /// Повертає false, якщо доступна точка NavMesh не знайдена.
        /// </summary>
        private bool EnableNavigationAtCurrentPosition()
        {
            if (_agent == null)
                return false;

            if (!TrySampleNavMesh(transform.position, _navMeshSnapRadius, out NavMeshHit hit) &&
                !TrySampleNavMesh(transform.position, _navMeshFallbackSnapRadius, out hit))
                return false;

            Vector3 navPosition = hit.position;
            transform.position = navPosition;
            _agent.enabled = true;

            if (!_agent.Warp(navPosition) || !_agent.isOnNavMesh)
            {
                _agent.enabled = false;
                return false;
            }

            _agent.updatePosition = true;
            _agent.updateRotation = false;
            _agent.ResetPath();
            return true;
        }

        /// <summary>Обгортає NavMesh.SamplePosition, використовуючи areaMask агента та мінімально затиснутий радіус.</summary>
        private bool TrySampleNavMesh(Vector3 position, float radius, out NavMeshHit hit)
        {
            return NavMesh.SamplePosition(
                position,
                out hit,
                Mathf.Max(0.1f, radius),
                _agent.areaMask);
        }

        private void ApplyMoveAfterSpawn()
        {
            bool hasMoveCommand = _hasQueuedMoveCommand || _hasDefaultMoveCommand;

            if (!hasMoveCommand)
                return;

            Vector3 requestedDestination = _hasQueuedMoveCommand
                ? _queuedMoveDestination
                : _defaultMoveDestination;
            bool usesDefaultDestination = !_hasQueuedMoveCommand && _hasDefaultMoveCommand;

            _hasQueuedMoveCommand = false;
            _hasDefaultMoveCommand = false;

            if (!TryResolveMoveAfterSpawnDestination(requestedDestination, out Vector3 destination))
            {
                ReleaseMoveReservation();

                if (usesDefaultDestination)
                    ReleaseDefaultMoveReservation();

                return;
            }

            if (_agent.SetDestination(destination))
            {
                if (usesDefaultDestination)
                    BeginDefaultMoveReservation(destination);

                EventManager.RaiseUnitMoveCommand(gameObject, destination);
            }
            else if (usesDefaultDestination)
            {
                ReleaseMoveReservation();
                ReleaseDefaultMoveReservation();
            }
            else
            {
                ReleaseMoveReservation();
            }
        }

        private bool TryResolveMoveAfterSpawnDestination(Vector3 requestedDestination, out Vector3 destination)
        {
            destination = requestedDestination;

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return false;

            if (TrySampleNavMesh(requestedDestination, _navMeshSnapRadius, out NavMeshHit hit) ||
                TrySampleNavMesh(requestedDestination, _navMeshFallbackSnapRadius, out hit))
            {
                destination = hit.position;
            }

            NavMeshPath path = new NavMeshPath();

            if (!_agent.CalculatePath(destination, path))
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

            destination = reachablePoint;
            return true;
        }

        /// <summary>Вмикає або вимикає бойову логіку, не чіпаючи виділення під час виїзду з заводу.</summary>
        private void SetCombatEnabled(bool enabled)
        {
            if (_combat != null)
                _combat.enabled = enabled;
        }

        private void OnUnitMoveCommand(GameObject unit, Vector3 destination)
        {
            if (unit != gameObject)
                return;

            ReserveMoveDestination(destination);

            if (_hasActiveDefaultMoveReservation)
            {
                Vector3 offset = destination - _activeDefaultMoveDestination;
                offset.y = 0f;

                if (offset.sqrMagnitude > 0.25f)
                    ReleaseDefaultMoveReservation();
            }
        }

        private void ReserveMoveDestination(Vector3 destination)
        {
            UnitDestinationReservations.Reserve(gameObject, destination, ResolveMoveReservationRadius());

            if (_releaseMoveReservationCoroutine != null)
                StopCoroutine(_releaseMoveReservationCoroutine);

            if (_agent != null && _agent.enabled)
                _releaseMoveReservationCoroutine = StartCoroutine(ReleaseMoveReservationWhenReached(destination));
        }

        private IEnumerator ReleaseMoveReservationWhenReached(Vector3 destination)
        {
            while (_agent != null && _agent.enabled)
            {
                if (_agent.isOnNavMesh &&
                    !_agent.pathPending &&
                    (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.35f))
                {
                    break;
                }

                yield return null;
            }

            _releaseMoveReservationCoroutine = null;
            UnitDestinationReservations.Release(gameObject);
        }

        private void ReleaseMoveReservation()
        {
            if (_releaseMoveReservationCoroutine != null)
            {
                StopCoroutine(_releaseMoveReservationCoroutine);
                _releaseMoveReservationCoroutine = null;
            }

            UnitDestinationReservations.Release(gameObject);
        }

        private float ResolveMoveReservationRadius()
        {
            if (_agent != null)
                return Mathf.Max(0.1f, _agent.radius + 0.75f);

            return 1f;
        }

        private void BeginDefaultMoveReservation(Vector3 destination)
        {
            if (_releaseDefaultMoveReservation == null)
                return;

            _activeDefaultMoveDestination = destination;
            _hasActiveDefaultMoveReservation = true;

            if (_releaseDefaultMoveReservationCoroutine != null)
                StopCoroutine(_releaseDefaultMoveReservationCoroutine);

            _releaseDefaultMoveReservationCoroutine = StartCoroutine(ReleaseDefaultMoveReservationWhenReached());
        }

        private IEnumerator ReleaseDefaultMoveReservationWhenReached()
        {
            while (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                if (!_agent.pathPending &&
                    (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.25f))
                {
                    break;
                }

                yield return null;
            }

            ReleaseDefaultMoveReservation();
        }

        private void ReleaseDefaultMoveReservation()
        {
            if (_releaseDefaultMoveReservationCoroutine != null)
            {
                StopCoroutine(_releaseDefaultMoveReservationCoroutine);
                _releaseDefaultMoveReservationCoroutine = null;
            }

            if (_releaseDefaultMoveReservation == null)
                return;

            Vector3 reservationDestination = _hasActiveDefaultMoveReservation
                ? _activeDefaultMoveDestination
                : _defaultMoveDestination;

            _releaseDefaultMoveReservation.Invoke(reservationDestination);
            _releaseDefaultMoveReservation = null;
            _hasActiveDefaultMoveReservation = false;
        }

        /// <summary>Вмикає або вимикає всі кешовані Collider на юніті для коректного вибору та блокування зайнятих слотів.</summary>
        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null)
                return;

            foreach (Collider unitCollider in _colliders)
            {
                if (unitCollider != null)
                    unitCollider.enabled = enabled;
            }
        }
    }
}
