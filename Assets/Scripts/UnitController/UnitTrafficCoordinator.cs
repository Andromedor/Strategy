using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Strategy.Units
{
    /// <summary>
    /// Керує локальною RTS-поведінкою "дай дорогу": idle-юніти, які перекривають коридор руху союзника,
    /// отримують короткий службовий наказ від'їхати вбік, не перебиваючи власні накази рухомих юнітів.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class UnitTrafficCoordinator : MonoBehaviour
    {
        // Локальний runtime-реєстр активних координаторів: дозволяє перевіряти сусідні юніти без FindObjectOfType у gameplay-коді.
        private static readonly List<UnitTrafficCoordinator> All = new();

        [Header("Traffic Yield")]
        [SerializeField] private bool _enableTrafficYield = true;
        [SerializeField, Min(0.05f)] private float _trafficCheckInterval = 0.15f;
        [SerializeField, Min(1f)] private float _yieldLookAheadDistance = 9f;
        [SerializeField, Min(0.1f)] private float _corridorPadding = 0.85f;
        [SerializeField, Min(0.5f)] private float _yieldSideDistance = 6.5f;
        [SerializeField, Min(0.1f)] private float _yieldSampleRadius = 4f;
        [SerializeField, Min(0.5f)] private float _yieldMaxDuration = 3.5f;
        [SerializeField, Min(0.1f)] private float _yieldCooldown = 0.75f;
        [SerializeField, Range(0, 99)] private int _movingAvoidancePriority = 30;
        [SerializeField, Range(0, 99)] private int _idleAvoidancePriority = 55;
        [SerializeField, Range(0, 99)] private int _yieldAvoidancePriority = 75;

        private NavMeshAgent _agent;
        private UnitSpawnActivator _spawnActivator;
        private TeamComponent _team;
        private float _nextTrafficCheckTime;
        private float _holdUntilTime;
        private float _yieldUntilTime;
        private float _nextYieldAllowedTime;
        private bool _isYielding;

        public bool IsYielding => _isYielding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            All.Clear();
        }

        public static UnitTrafficCoordinator Ensure(GameObject unit)
        {
            if (unit == null || unit.GetComponent<NavMeshAgent>() == null)
                return null;

            UnitTrafficCoordinator coordinator = unit.GetComponent<UnitTrafficCoordinator>();
            return coordinator != null ? coordinator : unit.AddComponent<UnitTrafficCoordinator>();
        }

        public static bool RequestYieldForCorridor(
            GameObject requester,
            Vector3 from,
            Vector3 to,
            float corridorRadius)
        {
            if (requester == null)
                return false;

            Vector3 direction = to - from;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.01f)
                return false;

            float radius = Mathf.Max(0.1f, corridorRadius);
            bool corridorBlocked = false;

            // Коридор вважається зайнятим навіть тоді, коли blocker уже від'їжджає: requester коротко чекає і не штовхає його в перші кадри.
            for (int i = All.Count - 1; i >= 0; i--)
            {
                UnitTrafficCoordinator blocker = All[i];

                if (blocker == null)
                {
                    All.RemoveAt(i);
                    continue;
                }

                if (!blocker.IsRelevantBlockerFor(requester))
                    continue;

                float blockerRadius = blocker.ResolveAgentRadius();
                float distanceToCorridor = DistancePointToSegment2D(blocker.transform.position, from, to);

                if (distanceToCorridor > radius + blockerRadius)
                    continue;

                if (blocker._isYielding)
                {
                    corridorBlocked = true;
                    continue;
                }

                if (!blocker.IsIdleForTrafficYield())
                    continue;

                corridorBlocked = true;
                blocker.TryYieldFromCorridor(from, to, radius);
            }

            return corridorBlocked;
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();

            if (!All.Contains(this))
                All.Add(this);

            EventManager.OnUnitMoveCommand += OnUnitMoveCommand;
        }

        private void OnDisable()
        {
            All.Remove(this);
            EventManager.OnUnitMoveCommand -= OnUnitMoveCommand;
            ReleaseTrafficReservation();
        }

        private void Update()
        {
            if (!_enableTrafficYield || _agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            UpdateYieldState();

            if (Time.time < _nextTrafficCheckTime)
                return;

            _nextTrafficCheckTime = Time.time + _trafficCheckInterval;
            UpdateAvoidancePriority();

            if (!IsMovingWithOwnPath())
                return;

            Vector3 start = transform.position;
            Vector3 end = ResolveLookAheadPoint(start);
            float corridorRadius = ResolveAgentRadius() + _corridorPadding;

            if (RequestYieldForCorridor(gameObject, start, end, corridorRadius))
                _holdUntilTime = Time.time + _trafficCheckInterval * 1.5f;
        }

        private void LateUpdate()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            // LateUpdate навмисно перекриває рішення мотора за цей кадр, щоб requester не проштовхував idle-блокер, який уже отримав yield-наказ.
            if (Time.time < _holdUntilTime && IsMovingWithOwnPath())
                _agent.isStopped = true;
        }

        private void CacheComponents()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            if (_spawnActivator == null)
                _spawnActivator = GetComponent<UnitSpawnActivator>();

            if (_team == null)
                _team = GetComponent<TeamComponent>();
        }

        private void OnUnitMoveCommand(GameObject unit, Vector3 destination)
        {
            if (unit != gameObject)
                return;

            if (_isYielding)
                ReleaseTrafficReservation();

            _holdUntilTime = 0f;
        }

        private bool IsRelevantBlockerFor(GameObject requester)
        {
            if (!_enableTrafficYield || requester == null || requester == gameObject || !isActiveAndEnabled)
                return false;

            if (_spawnActivator != null && _spawnActivator.IsSpawning)
                return false;

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return false;

            TeamComponent requesterTeam = requester.GetComponent<TeamComponent>();
            if (_team != null && requesterTeam != null && _team.Team != requesterTeam.Team)
                return false;

            return true;
        }

        private bool IsIdleForTrafficYield()
        {
            if (_agent == null || _agent.pathPending)
                return false;

            if (!_agent.hasPath)
                return true;

            return _agent.remainingDistance <= _agent.stoppingDistance + 0.35f;
        }

        private bool IsMovingWithOwnPath()
        {
            if (_isYielding || _spawnActivator != null && _spawnActivator.IsSpawning)
                return false;

            if (_agent == null || _agent.pathPending || !_agent.hasPath)
                return false;

            return _agent.remainingDistance > _agent.stoppingDistance + 0.35f;
        }

        private bool TryYieldFromCorridor(Vector3 from, Vector3 to, float corridorRadius)
        {
            if (Time.time < _nextYieldAllowedTime)
                return false;

            if (!TryFindYieldDestination(from, to, corridorRadius, out Vector3 destination))
                return false;

            _agent.isStopped = false;

            if (!_agent.SetDestination(destination))
                return false;

            _isYielding = true;
            _yieldUntilTime = Time.time + _yieldMaxDuration;
            _agent.avoidancePriority = _yieldAvoidancePriority;
            // Службовий від'їзд також бронює точку, інакше інший юніт може отримати той самий side-step.
            UnitDestinationReservations.Reserve(gameObject, destination, ResolveAgentRadius() + _corridorPadding);
            return true;
        }

        private bool TryFindYieldDestination(
            Vector3 from,
            Vector3 to,
            float corridorRadius,
            out Vector3 destination)
        {
            destination = transform.position;

            Vector3 pathDirection = to - from;
            pathDirection.y = 0f;

            if (pathDirection.sqrMagnitude <= 0.01f)
                return false;

            pathDirection.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, pathDirection).normalized;
            Vector3 closestOnPath = ClosestPointOnSegment2D(transform.position, from, to);
            Vector3 awayFromPath = transform.position - closestOnPath;
            awayFromPath.y = 0f;

            if (awayFromPath.sqrMagnitude <= 0.01f)
                awayFromPath = right;
            else
                awayFromPath.Normalize();

            // Спершу пробуємо напрямок від коридору, потім бокові варіанти: blocker від'їжджає з дороги, а не випадково здає назад.
            Vector3[] directions =
            {
                awayFromPath,
                right,
                -right,
                (right - pathDirection * 0.35f).normalized,
                (-right - pathDirection * 0.35f).normalized,
                -pathDirection
            };

            float ownRadius = ResolveAgentRadius();
            float baseDistance = Mathf.Max(_yieldSideDistance, corridorRadius + ownRadius + _corridorPadding);

            for (int ring = 1; ring <= 2; ring++)
            {
                float distance = baseDistance * ring;

                for (int i = 0; i < directions.Length; i++)
                {
                    Vector3 candidate = transform.position + directions[i] * distance;

                    if (!NavMesh.SamplePosition(
                            candidate,
                            out NavMeshHit hit,
                            _yieldSampleRadius,
                            _agent.areaMask))
                    {
                        continue;
                    }

                    if (DistancePointToSegment2D(hit.position, from, to) <= corridorRadius + ownRadius * 0.5f)
                        continue;

                    if (IsYieldDestinationOccupied(hit.position, ownRadius))
                        continue;

                    NavMeshPath path = new NavMeshPath();
                    if (!_agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                        continue;

                    destination = hit.position;
                    return true;
                }
            }

            return false;
        }

        private bool IsYieldDestinationOccupied(Vector3 destination, float ownRadius)
        {
            if (UnitDestinationReservations.IsReservedByOther(
                    gameObject,
                    destination,
                    ownRadius + _corridorPadding))
            {
                return true;
            }

            for (int i = All.Count - 1; i >= 0; i--)
            {
                UnitTrafficCoordinator other = All[i];

                if (other == null)
                {
                    All.RemoveAt(i);
                    continue;
                }

                if (other == this || other._agent == null)
                    continue;

                Vector3 offset = other.transform.position - destination;
                offset.y = 0f;
                float blockedDistance = ownRadius + other.ResolveAgentRadius() + _corridorPadding;

                if (offset.sqrMagnitude <= blockedDistance * blockedDistance)
                    return true;
            }

            return false;
        }

        private void UpdateYieldState()
        {
            if (!_isYielding)
                return;

            if (Time.time >= _yieldUntilTime)
            {
                ReleaseTrafficReservation();
                return;
            }

            if (_agent.pathPending)
                return;

            if (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.25f)
                ReleaseTrafficReservation();
        }

        private void ReleaseTrafficReservation()
        {
            if (!_isYielding)
                return;

            _isYielding = false;
            _nextYieldAllowedTime = Time.time + _yieldCooldown;
            UnitDestinationReservations.Release(gameObject);
        }

        private Vector3 ResolveLookAheadPoint(Vector3 start)
        {
            Vector3 target = _agent.steeringTarget;
            target.y = start.y;

            if ((target - start).sqrMagnitude <= 0.25f)
                target = _agent.destination;

            Vector3 offset = target - start;
            offset.y = 0f;

            float distance = offset.magnitude;
            if (distance <= 0.01f)
                return start;

            float lookAhead = Mathf.Min(distance, Mathf.Max(1f, _yieldLookAheadDistance));
            return start + offset.normalized * lookAhead;
        }

        private void UpdateAvoidancePriority()
        {
            if (_agent == null)
                return;

            if (_isYielding)
                _agent.avoidancePriority = _yieldAvoidancePriority;
            else if (IsMovingWithOwnPath())
                _agent.avoidancePriority = _movingAvoidancePriority;
            else
                _agent.avoidancePriority = _idleAvoidancePriority;
        }

        private float ResolveAgentRadius()
        {
            return _agent != null ? Mathf.Max(0.1f, _agent.radius) : 1f;
        }

        private static float DistancePointToSegment2D(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
        {
            Vector3 closest = ClosestPointOnSegment2D(point, segmentStart, segmentEnd);
            return Vector3.Distance(
                new Vector3(point.x, 0f, point.z),
                new Vector3(closest.x, 0f, closest.z));
        }

        private static Vector3 ClosestPointOnSegment2D(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
        {
            Vector3 start = new Vector3(segmentStart.x, 0f, segmentStart.z);
            Vector3 end = new Vector3(segmentEnd.x, 0f, segmentEnd.z);
            Vector3 flatPoint = new Vector3(point.x, 0f, point.z);
            Vector3 segment = end - start;

            if (segment.sqrMagnitude <= 0.0001f)
                return segmentStart;

            float t = Vector3.Dot(flatPoint - start, segment) / segment.sqrMagnitude;
            t = Mathf.Clamp01(t);
            Vector3 closest = start + segment * t;
            return new Vector3(closest.x, point.y, closest.z);
        }
    }
}
