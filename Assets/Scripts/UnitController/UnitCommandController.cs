using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Strategy.Units
{
    /// <summary>
    /// Обробляє весь ввід гравця для виділення юнітів та видачі наказів.
    /// Перетягування лівою кнопкою виконує мульти-виділення через BoxCast; правий клік по ворогу
    /// видає наказ атаки; правий клік по землі видає наказ переміщення зі зміщеннями формації у шаховому порядку.
    /// </summary>
    public class UnitCommandController : MonoBehaviour
    {
        [SerializeField] private GameObject _cubePrefab;
        [SerializeField] private LayerMask _enemyMask;
        [SerializeField] private LayerMask _cubeMask;
        [SerializeField] private LayerMask _selectedLayerMask;
        [SerializeField] private List<GameObject> _selections = new();
        [SerializeField] private float _formationSpacing = 4f;
        [SerializeField] private float _navMeshSampleRadius = 3f;
        [SerializeField] private float _navMeshFallbackSampleRadius = 8f;
        [SerializeField, Min(0f)] private float _destinationClearancePadding = 0.75f;
        [SerializeField, Min(1)] private int _destinationSearchRings = 4;
        [SerializeField, Min(4)] private int _destinationCandidatesPerRing = 8;
        [SerializeField] private float _selectionDragThreshold = 0.35f;
        [SerializeField] private LayerMask _destinationBlockerMask;

        private readonly Collider[] _destinationOverlapBuffer = new Collider[32];
        private readonly List<Vector3> _reservedMoveDestinations = new();
        private UnityEngine.Camera _camera;
        private GameObject _currentSelection;
        private Vector3 _startPoint;
        private bool _isSelectionPressActive;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _selections ??= new List<GameObject>();

            if (_destinationBlockerMask.value == 0)
                _destinationBlockerMask = LayerMask.GetMask("PlayerUnit", "EnemyUnit");
        }

        private void Update()
        {
            if (Mouse.current == null)
                return;

            if (Mouse.current.rightButton.wasPressedThisFrame && _selections.Count > 0)
                ControlUnits();

            if (Mouse.current.leftButton.wasPressedThisFrame)
                StartSelectionPress();

            if (Mouse.current.leftButton.isPressed && _isSelectionPressActive)
                UpdateSelectionPress();

            if (Mouse.current.leftButton.wasReleasedThisFrame)
                EndSelection();
        }

        /// <summary>Кидає промінь від позиції правого кліку до шарів ворогів та землі, направляючи до відповідного обробника — атаки або переміщення.</summary>
        private void ControlUnits()
        {
            if (_selections == null || _selections.Count == 0 || !TryCreateMouseRay(out Ray ray))
                return;

            if (Physics.Raycast(ray, out RaycastHit enemyHit, 1000f, _enemyMask))
            {
                CommandAttack(enemyHit.transform);
                return;
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, _cubeMask))
                CommandMove(groundHit.point);
        }

        /// <summary>Призначає вручну обрану ціль атаки для UnitCombat кожного вибраного юніта та викидає OnUnitAttackTargetChanged.</summary>
        private void CommandAttack(Transform enemy)
        {
            foreach (GameObject selection in _selections)
            {
                if (selection == null)
                    continue;

                UnitCombat attack = selection.GetComponent<UnitCombat>();

                if (attack == null)
                    continue;

                attack.SetManualAttackTarget(enemy);
                EventManager.RaiseUnitAttackTargetChanged(selection, enemy);
            }
        }

        /// <summary>
        /// Видає наказ усім вибраним юнітам переміститися до позицій формації у шаховому порядку навколо targetPoint,
        /// прив'язуючи кожен пункт призначення до доступної точки NavMesh перед викликом SetDestination.
        /// </summary>
        private void CommandMove(Vector3 targetPoint)
        {
            GameObject firstUnit = GetFirstValidSelection();

            if (firstUnit == null)
                return;

            Vector3 dir = (targetPoint - firstUnit.transform.position).normalized;

            if (dir.sqrMagnitude < 0.01f)
                dir = firstUnit.transform.forward;

            Quaternion rotation = Quaternion.LookRotation(dir);
            float formationSpacing = ResolveFormationSpacing();
            int index = 0;
            _reservedMoveDestinations.Clear();

            foreach (GameObject selection in _selections)
            {
                if (selection == null)
                    continue;

                int formationIndex = index++;
                Vector3 destination = formationIndex == 0
                    ? targetPoint
                    : GetChessFormationPosition(targetPoint, formationIndex, formationSpacing, rotation);

                NavMeshAgent agent = selection.GetComponent<NavMeshAgent>();
                UnitSpawnActivator spawnActivator = selection.GetComponent<UnitSpawnActivator>();

                if (spawnActivator != null && spawnActivator.IsSpawning)
                {
                    Vector3 queuedDestination = ResolveQueuedSpawnMoveDestination(agent, destination);
                    spawnActivator.QueueMoveAfterSpawn(queuedDestination);
                    ReserveMoveDestination(agent, queuedDestination);
                    EventManager.RaiseUnitMoveCommand(selection, queuedDestination);
                    continue;
                }

                if (agent == null)
                    continue;

                if (!TryResolveNavMeshDestination(agent, destination, out destination))
                    continue;

                if (!agent.SetDestination(destination))
                    continue;

                ReserveMoveDestination(agent, destination);
                EventManager.RaiseUnitMoveCommand(selection, destination);
            }
        }

        /// <summary>
        /// Зондує NavMesh поблизу requestedDestination (із запасним радіусом) та перевіряє досяжність шляху.
        /// Повертається до останнього досяжного кута, коли існує лише частковий шлях.
        /// </summary>
        private bool TryResolveNavMeshDestination(
            NavMeshAgent agent,
            Vector3 requestedDestination,
            out Vector3 resolvedDestination)
        {
            resolvedDestination = requestedDestination;

            if (agent == null || !agent.enabled || !TryEnsureAgentOnNavMesh(agent))
                return false;

            return TryFindReachableMoveDestination(agent, requestedDestination, out resolvedDestination);
        }

        private Vector3 ResolveQueuedSpawnMoveDestination(NavMeshAgent agent, Vector3 requestedDestination)
        {
            if (agent == null)
                return requestedDestination;

            if (TryFindOpenMoveDestination(agent, requestedDestination, out Vector3 destination))
                return destination;

            return requestedDestination;
        }

        private bool TryFindReachableMoveDestination(
            NavMeshAgent agent,
            Vector3 requestedDestination,
            out Vector3 resolvedDestination)
        {
            foreach (Vector3 candidate in GetDestinationCandidates(agent, requestedDestination))
            {
                if (!TrySampleDestination(agent, candidate, _navMeshSampleRadius, out NavMeshHit navHit) &&
                    !TrySampleDestination(agent, candidate, _navMeshFallbackSampleRadius, out navHit))
                {
                    continue;
                }

                float clearanceRadius = ResolveDestinationClearanceRadius(agent);

                if (IsDestinationBlocked(agent, navHit.position, clearanceRadius))
                    continue;

                if (TryCalculateReachableDestination(agent, navHit.position, clearanceRadius, out resolvedDestination))
                    return true;
            }

            resolvedDestination = requestedDestination;
            return false;
        }

        private bool TryFindOpenMoveDestination(
            NavMeshAgent agent,
            Vector3 requestedDestination,
            out Vector3 resolvedDestination)
        {
            foreach (Vector3 candidate in GetDestinationCandidates(agent, requestedDestination))
            {
                if (!TrySampleDestination(agent, candidate, _navMeshSampleRadius, out NavMeshHit navHit) &&
                    !TrySampleDestination(agent, candidate, _navMeshFallbackSampleRadius, out navHit))
                {
                    continue;
                }

                if (IsDestinationBlocked(agent, navHit.position, ResolveDestinationClearanceRadius(agent)))
                    continue;

                resolvedDestination = navHit.position;
                return true;
            }

            resolvedDestination = requestedDestination;
            return false;
        }

        private bool TryCalculateReachableDestination(
            NavMeshAgent agent,
            Vector3 requestedDestination,
            float clearanceRadius,
            out Vector3 resolvedDestination)
        {
            resolvedDestination = requestedDestination;
            NavMeshPath path = new NavMeshPath();

            if (!agent.CalculatePath(resolvedDestination, path))
                return false;

            if (path.status == NavMeshPathStatus.PathComplete)
                return true;

            if (path.status != NavMeshPathStatus.PathPartial ||
                path.corners == null ||
                path.corners.Length < 2)
                return false;

            Vector3 reachablePoint = path.corners[path.corners.Length - 1];
            float minMoveDistance = Mathf.Max(0.25f, agent.stoppingDistance + 0.1f);

            if ((reachablePoint - agent.transform.position).sqrMagnitude <= minMoveDistance * minMoveDistance)
                return false;

            if (IsDestinationBlocked(agent, reachablePoint, clearanceRadius))
                return false;

            resolvedDestination = reachablePoint;
            return true;
        }

        private IEnumerable<Vector3> GetDestinationCandidates(NavMeshAgent agent, Vector3 center)
        {
            yield return center;

            int rings = Mathf.Max(1, _destinationSearchRings);
            int candidatesPerRing = Mathf.Max(4, _destinationCandidatesPerRing);
            float spacing = ResolveDestinationSpacing(agent);

            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = spacing * ring;
                float ringOffset = ring % 2 == 0 ? 0f : 0.5f;

                for (int i = 0; i < candidatesPerRing; i++)
                {
                    float angle = ((i + ringOffset) / candidatesPerRing) * Mathf.PI * 2f;
                    yield return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }
            }
        }

        private bool IsDestinationBlocked(NavMeshAgent agent, Vector3 destination, float clearanceRadius)
        {
            float reservedDistance = clearanceRadius * 2f;
            float reservedDistanceSqr = reservedDistance * reservedDistance;

            foreach (Vector3 reservedDestination in _reservedMoveDestinations)
            {
                Vector3 offset = destination - reservedDestination;
                offset.y = 0f;

                if (offset.sqrMagnitude <= reservedDistanceSqr)
                    return true;
            }

            if (_destinationBlockerMask.value == 0)
                return UnitDestinationReservations.IsReservedByOther(
                    agent != null ? agent.gameObject : null,
                    destination,
                    clearanceRadius);

            if (UnitDestinationReservations.IsReservedByOther(
                    agent != null ? agent.gameObject : null,
                    destination,
                    clearanceRadius))
            {
                return true;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                destination + Vector3.up * 0.5f,
                clearanceRadius,
                _destinationOverlapBuffer,
                _destinationBlockerMask,
                QueryTriggerInteraction.Ignore);

            Transform ownRoot = agent != null ? agent.transform.root : null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _destinationOverlapBuffer[i];

                if (hit == null || !hit.enabled)
                    continue;

                if (ownRoot != null && hit.transform.root == ownRoot)
                    continue;

                return true;
            }

            return false;
        }

        private void ReserveMoveDestination(NavMeshAgent agent, Vector3 destination)
        {
            if (agent == null)
                return;

            _reservedMoveDestinations.Add(destination);
        }

        private float ResolveFormationSpacing()
        {
            float spacing = Mathf.Max(0.1f, _formationSpacing);

            foreach (GameObject selection in _selections)
            {
                if (selection == null)
                    continue;

                NavMeshAgent agent = selection.GetComponent<NavMeshAgent>();

                if (agent == null)
                    continue;

                spacing = Mathf.Max(spacing, ResolveDestinationSpacing(agent));
            }

            return spacing;
        }

        private float ResolveDestinationSpacing(NavMeshAgent agent)
        {
            if (agent == null)
                return Mathf.Max(0.1f, _formationSpacing);

            return Mathf.Max(_formationSpacing, agent.radius * 2f + _destinationClearancePadding);
        }

        private float ResolveDestinationClearanceRadius(NavMeshAgent agent)
        {
            float radius = agent != null ? agent.radius : _formationSpacing * 0.5f;
            return Mathf.Max(0.1f, radius + _destinationClearancePadding);
        }

        /// <summary>Обгортає NavMesh.SamplePosition, обмежений areaMask агента, із затиснутим мінімальним радіусом.</summary>
        private static bool TrySampleDestination(
            NavMeshAgent agent,
            Vector3 destination,
            float radius,
            out NavMeshHit hit)
        {
            return NavMesh.SamplePosition(
                destination,
                out hit,
                Mathf.Max(0.05f, radius),
                agent.areaMask);
        }

        /// <summary>Переміщує агента до найближчої точки NavMesh, якщо він якимось чином зійшов з поверхні; повертає false, якщо відновлення не вдалося.</summary>
        private bool TryEnsureAgentOnNavMesh(NavMeshAgent agent)
        {
            if (agent.isOnNavMesh)
                return true;

            if (!NavMesh.SamplePosition(
                    agent.transform.position,
                    out NavMeshHit hit,
                    _navMeshFallbackSampleRadius,
                    agent.areaMask))
            {
                return false;
            }

            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        /// <summary>Обчислює зміщення формації у шаховому порядку (почергово ліво/право, відступаючі ряди) для юніта з індексом відносно центральної точки переміщення.</summary>
        private static Vector3 GetChessFormationPosition(
            Vector3 center,
            int index,
            float spacing,
            Quaternion rotation)
        {
            int formationIndex = index - 1;
            int row = formationIndex / 2 + 1;
            int side = formationIndex % 2 == 0 ? -1 : 1;
            float x = side * spacing * 0.5f;

            if (row % 2 == 0)
                x += side * spacing * 0.5f;

            float z = -row * spacing;

            return center + rotation * new Vector3(x, 0f, z);
        }

        private GameObject GetFirstValidSelection()
        {
            foreach (GameObject obj in _selections)
            {
                if (obj != null)
                    return obj;
            }

            return null;
        }

        /// <summary>Знімає виділення з усіх юнітів та записує початкову точку попадання на землю для потенційного прямокутника перетягування.</summary>
        private void StartSelectionPress()
        {
            if (IsPointerOverUi() || !RaycastToGround(out Vector3 hitPoint))
                return;

            DeselectAll();
            _startPoint = hitPoint;
            _isSelectionPressActive = true;
        }

        /// <summary>Ініціює куб виділення перетягуванням, як тільки переміщення перевищує поріг, потім змінює його розмір для відстеження курсора.</summary>
        private void UpdateSelectionPress()
        {
            if (!RaycastToGround(out Vector3 currentPoint))
                return;

            if (_currentSelection == null)
            {
                Vector3 delta = currentPoint - _startPoint;
                delta.y = 0f;

                if (delta.sqrMagnitude < _selectionDragThreshold * _selectionDragThreshold)
                    return;

                BeginSelectionDrag();
            }

            UpdateSelectionVisual(currentPoint);
        }

        /// <summary>Спавнить префаб куба — візуальний прямокутник виділення — у початковій позиції перетягування.</summary>
        private void BeginSelectionDrag()
        {
            if (_cubePrefab == null)
                return;

            _currentSelection = Instantiate(
                _cubePrefab,
                new Vector3(_startPoint.x, 1f, _startPoint.z),
                Quaternion.identity,
                RuntimeObjectContainer.Get("Selection"));
        }

        /// <summary>Переміщує та масштабує куб виділення так, щоб він охоплював від початкової точки перетягування до поточної позиції курсора.</summary>
        private void UpdateSelectionVisual(Vector3 currentPoint)
        {
            if (_currentSelection == null)
                return;

            Vector3 center = (_startPoint + currentPoint) * 0.5f;
            Vector3 size = new Vector3(
                Mathf.Abs(currentPoint.x - _startPoint.x),
                1f,
                Mathf.Abs(currentPoint.z - _startPoint.z));

            _currentSelection.transform.position = new Vector3(center.x, 1f, center.z);
            _currentSelection.transform.rotation = Quaternion.identity;
            _currentSelection.transform.localScale = size;
        }

        /// <summary>Використовує OverlapBox з межами куба виділення для пошуку юнітів гравця всередині та додає їх до _selections через RaiseUnitSelected.</summary>
        private void EndSelection()
        {
            _isSelectionPressActive = false;

            if (_currentSelection == null)
                return;

            Vector3 halfExtents = _currentSelection.transform.localScale * 0.5f;
            halfExtents.y = 1f;

            Collider[] hits = Physics.OverlapBox(
                _currentSelection.transform.position,
                halfExtents,
                Quaternion.identity,
                _selectedLayerMask);

            foreach (Collider hit in hits)
            {
                if (hit == null || hit.CompareTag("Enemy"))
                    continue;

                GameObject unit = hit.transform.gameObject;
                if (_selections.Contains(unit))
                    continue;

                _selections.Add(unit);
                EventManager.RaiseUnitSelected(unit);
            }

            Destroy(_currentSelection);
            _currentSelection = null;
        }

        /// <summary>Викидає RaiseUnitDeselected для кожного вибраного юніта та очищає список виділення.</summary>
        private void DeselectAll()
        {
            foreach (GameObject selection in _selections)
            {
                if (selection != null)
                    EventManager.RaiseUnitDeselected(selection);
            }

            _selections.Clear();
        }

        /// <summary>Кидає промінь із камери через курсор миші до маски шару землі/куба; повертає точку попадання.</summary>
        private bool RaycastToGround(out Vector3 point)
        {
            point = Vector3.zero;

            if (!TryCreateMouseRay(out Ray ray))
                return false;

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _cubeMask))
                return false;

            point = hit.point;
            return true;
        }

        /// <summary>Створює промінь у світовому просторі з камери через поточну позицію миші; повертає false, якщо камера недоступна.</summary>
        private bool TryCreateMouseRay(out Ray ray)
        {
            if (_camera == null)
                _camera = GetComponent<UnityEngine.Camera>();

            if (_camera == null || Mouse.current == null)
            {
                ray = default;
                return false;
            }

            ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return true;
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
