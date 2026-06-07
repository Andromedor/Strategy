using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Core;
using Strategy.UI;
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
        [SerializeField, Min(1f)] private float _selectionDragScreenThreshold = 6f;
        [SerializeField] private SelectionDragBoxUI _selectionDragBox;
        [SerializeField] private LayerMask _destinationBlockerMask;

        private readonly Collider[] _destinationOverlapBuffer = new Collider[32];
        private readonly List<Vector3> _reservedMoveDestinations = new();
        private readonly List<GameObject> _dragUnitBuffer = new();
        private readonly List<GameObject> _dragBuildingBuffer = new();
        private readonly List<Vector3> _dragGroundCornerBuffer = new();
        private readonly List<GameObject> _commandTargetBuffer = new();
        private UnityEngine.Camera _camera;
        private Vector3 _startPoint;
        private Vector2 _dragStartScreenPoint;
        private Vector2 _dragCurrentScreenPoint;
        private bool _hasDragStartGroundPoint;
        private bool _isSelectionPressActive;
        private bool _isCtrlSelectionPress;
        private static int _lastHandledLeftReleaseFrame = -1;
        private static bool _isSelectionDragActive;

        private enum SelectionKind
        {
            None,
            Unit,
            Building
        }

        public static bool DidHandleLeftReleaseThisFrame => _lastHandledLeftReleaseFrame == Time.frameCount;
        public static bool IsSelectionDragActive => _isSelectionDragActive;

        public int SelectedUnitCount
        {
            get
            {
                RemoveDeadSelections();
                int count = 0;

                for (int i = 0; i < _selections.Count; i++)
                {
                    if (IsUnitSelection(_selections[i]))
                        count++;
                }

                return count;
            }
        }

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _selections ??= new List<GameObject>();

            if (_destinationBlockerMask.value == 0)
                _destinationBlockerMask = LayerMask.GetMask("PlayerUnit", "EnemyUnit");
        }

        private void OnEnable()
        {
            EventManager.OnUnitDestroyed += RemoveDestroyedSelection;
            EventManager.OnBuildingDestroyed += RemoveDestroyedSelection;
        }

        private void OnDisable()
        {
            EventManager.OnUnitDestroyed -= RemoveDestroyedSelection;
            EventManager.OnBuildingDestroyed -= RemoveDestroyedSelection;
            HideSelectionVisual();
            _isSelectionPressActive = false;
            _isSelectionDragActive = false;
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

        /// <summary>Копіює поточний живий вибір у наданий список, не відкриваючи внутрішню колекцію для змін ззовні.</summary>
        public void CopySelectedUnits(List<GameObject> target)
        {
            if (target == null)
                return;

            target.Clear();
            RemoveDeadSelections();

            for (int i = 0; i < _selections.Count; i++)
            {
                GameObject selection = _selections[i];
                if (IsUnitSelection(selection))
                    target.Add(selection);
            }
        }

        public void CopySelectedObjects(List<GameObject> target)
        {
            if (target == null)
                return;

            target.Clear();
            RemoveDeadSelections();

            for (int i = 0; i < _selections.Count; i++)
                target.Add(_selections[i]);
        }

        /// <summary>Знімає поточне виділення через ті самі події, які використовує звичайний gameplay-ввід.</summary>
        public void ClearSelection()
        {
            DeselectAll();
        }

        /// <summary>
        /// Перевиділяє набір юнітів із зовнішньої системи, наприклад control group, зберігаючи єдиний подієвий шлях selection-стану.
        /// </summary>
        public void SelectUnits(IEnumerable<GameObject> units)
        {
            DeselectAll(false);

            if (units == null)
            {
                PublishSelectionChanged();
                return;
            }

            foreach (GameObject unit in units)
            {
                if (IsUnitSelection(unit))
                    TryAddSelection(unit, false);
            }

            PublishSelectionChanged();
        }

        public void SelectObjects(IEnumerable<GameObject> objects)
        {
            DeselectAll(false);

            if (objects == null)
            {
                PublishSelectionChanged();
                return;
            }

            foreach (GameObject selection in objects)
                TryAddSelection(selection, false);

            PublishSelectionChanged();
        }

        /// <summary>Кидає промінь від позиції правого кліку до шарів ворогів та землі, направляючи до відповідного обробника — атаки або переміщення.</summary>
        private void ControlUnits()
        {
            RemoveDeadSelections();

            if (_selections == null || _selections.Count == 0 || !TryCreateMouseRay(out Ray ray))
                return;

            if (TryRaycastAttackTarget(ray, out Transform attackTarget))
            {
                DispatchAttackCommand(attackTarget);
                return;
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, _cubeMask))
                DispatchMoveCommand(groundHit.point);
        }

        private void DispatchAttackCommand(Transform enemy)
        {
            GameObject[] targets = SnapshotCommandableUnits();
            PlayerCommand command = PlayerCommand.AttackTarget(
                LocalPlayerContext.LocalTeam,
                LocalPlayerContext.LocalPlayerId,
                targets,
                enemy);

            CommandDispatcher.Dispatch(command, ExecuteAttackCommand);
        }

        private void ExecuteAttackCommand(PlayerCommand command)
        {
            CommandAttack(command.TargetTransform);
        }

        /// <summary>Призначає вручну обрану ціль атаки для UnitCombat кожного вибраного юніта та викидає OnUnitAttackTargetChanged.</summary>
        private void CommandAttack(Transform enemy)
        {
            if (!IsAttackableTarget(enemy))
                return;

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

        private bool TryRaycastAttackTarget(Ray ray, out Transform target)
        {
            target = null;
            int attackMask = _enemyMask.value | LayerMask.GetMask("Building");

            if (attackMask == 0)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                1000f,
                attackMask,
                QueryTriggerInteraction.Collide);

            float closestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Transform candidate = hits[i].transform;

                if (!IsAttackableTarget(candidate) || hits[i].distance >= closestDistance)
                    continue;

                target = candidate;
                closestDistance = hits[i].distance;
            }

            return target != null;
        }

        private bool IsAttackableTarget(Transform target)
        {
            if (target == null || target.GetComponentInParent<Outpost>() != null)
                return false;

            if (target.GetComponentInParent<IDamageable>() == null)
                return false;

            ITeam targetTeam = target.GetComponentInParent<ITeam>();

            if (targetTeam == null)
                return false;

            for (int i = 0; i < _selections.Count; i++)
            {
                GameObject selection = _selections[i];
                UnitCombat combat = selection != null ? selection.GetComponent<UnitCombat>() : null;

                if (combat != null && TeamRelations.AreHostile(combat.Team, targetTeam.Team))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Видає наказ усім вибраним юнітам переміститися до позицій формації у шаховому порядку навколо targetPoint,
        /// прив'язуючи кожен пункт призначення до доступної точки NavMesh перед викликом SetDestination.
        /// </summary>
        private void DispatchMoveCommand(Vector3 targetPoint)
        {
            GameObject[] targets = SnapshotCommandableUnits();
            PlayerCommand command = PlayerCommand.MoveUnits(
                LocalPlayerContext.LocalTeam,
                LocalPlayerContext.LocalPlayerId,
                targets,
                targetPoint);

            CommandDispatcher.Dispatch(command, ExecuteMoveCommand);
        }

        private void ExecuteMoveCommand(PlayerCommand command)
        {
            CommandMove(command.TargetPosition);
        }

        private void CommandMove(Vector3 targetPoint)
        {
            GameObject firstUnit = GetFirstCommandableUnit();

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

                NavMeshAgent agent = selection.GetComponent<NavMeshAgent>();
                UnitSpawnActivator spawnActivator = selection.GetComponent<UnitSpawnActivator>();

                if (agent == null && spawnActivator == null)
                    continue;

                int formationIndex = index++;
                Vector3 destination = formationIndex == 0
                    ? targetPoint
                    : GetChessFormationPosition(targetPoint, formationIndex, formationSpacing, rotation);

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

        private GameObject GetFirstCommandableUnit()
        {
            foreach (GameObject obj in _selections)
            {
                if (obj != null &&
                    (obj.GetComponent<NavMeshAgent>() != null ||
                     obj.GetComponent<UnitSpawnActivator>() != null))
                {
                    return obj;
                }
            }

            return null;
        }

        /// <summary>Знімає виділення з усіх юнітів та записує початкову точку попадання на землю для потенційного прямокутника перетягування.</summary>
        private GameObject[] SnapshotCommandableUnits()
        {
            _commandTargetBuffer.Clear();

            for (int i = 0; i < _selections.Count; i++)
            {
                GameObject selection = _selections[i];

                if (selection == null)
                    continue;

                if (selection.GetComponent<NavMeshAgent>() == null &&
                    selection.GetComponent<UnitSpawnActivator>() == null &&
                    selection.GetComponent<UnitCombat>() == null)
                {
                    continue;
                }

                _commandTargetBuffer.Add(selection);
            }

            return _commandTargetBuffer.ToArray();
        }

        private void StartSelectionPress()
        {
            if (BuildingPlacementManager.IsPlacing || IsPointerOverUi() || !TryCreateMouseRay(out _))
                return;

            _dragStartScreenPoint = Mouse.current.position.ReadValue();
            _dragCurrentScreenPoint = _dragStartScreenPoint;
            _hasDragStartGroundPoint = RaycastToGround(out _startPoint);
            _isCtrlSelectionPress = IsCtrlPressed();
            _isSelectionPressActive = true;
        }

        /// <summary>Ініціює куб виділення перетягуванням, як тільки переміщення перевищує поріг, потім змінює його розмір для відстеження курсора.</summary>
        private void UpdateSelectionPress()
        {
            _dragCurrentScreenPoint = Mouse.current.position.ReadValue();

            if (!_isSelectionDragActive)
            {
                float screenThreshold = Mathf.Max(
                    _selectionDragScreenThreshold,
                    _selectionDragThreshold * 16f);

                if ((_dragCurrentScreenPoint - _dragStartScreenPoint).sqrMagnitude <
                    screenThreshold * screenThreshold)
                {
                    return;
                }

                BeginSelectionDrag();
            }

            UpdateSelectionVisual(_dragCurrentScreenPoint);
        }

        /// <summary>Спавнить префаб куба — візуальний прямокутник виділення — у початковій позиції перетягування.</summary>
        private void BeginSelectionDrag()
        {
            _isSelectionDragActive = true;
            _selectionDragBox?.Show(_dragStartScreenPoint, _dragCurrentScreenPoint);
        }

        /// <summary>Переміщує та масштабує куб виділення так, щоб він охоплював від початкової точки перетягування до поточної позиції курсора.</summary>
        private void UpdateSelectionVisual(Vector2 currentScreenPoint)
        {
            _selectionDragBox?.UpdateBox(_dragStartScreenPoint, currentScreenPoint);
        }

        /// <summary>Використовує OverlapBox з межами куба виділення для пошуку юнітів гравця всередині та додає їх до _selections через RaiseUnitSelected.</summary>
        private void HideSelectionVisual()
        {
            _selectionDragBox?.Hide();
        }

        private Rect GetDragScreenRect()
        {
            Vector2 min = Vector2.Min(_dragStartScreenPoint, _dragCurrentScreenPoint);
            Vector2 max = Vector2.Max(_dragStartScreenPoint, _dragCurrentScreenPoint);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private bool TryBuildDragWorldBroadphase(Rect screenRect, out Vector3 center, out Vector3 halfExtents)
        {
            _dragGroundCornerBuffer.Clear();

            TryAddGroundPoint(new Vector2(screenRect.xMin, screenRect.yMin));
            TryAddGroundPoint(new Vector2(screenRect.xMin, screenRect.yMax));
            TryAddGroundPoint(new Vector2(screenRect.xMax, screenRect.yMin));
            TryAddGroundPoint(new Vector2(screenRect.xMax, screenRect.yMax));

            if (_dragGroundCornerBuffer.Count == 0)
            {
                center = Vector3.zero;
                halfExtents = Vector3.zero;
                return false;
            }

            Vector3 min = _dragGroundCornerBuffer[0];
            Vector3 max = _dragGroundCornerBuffer[0];

            for (int i = 1; i < _dragGroundCornerBuffer.Count; i++)
            {
                min = Vector3.Min(min, _dragGroundCornerBuffer[i]);
                max = Vector3.Max(max, _dragGroundCornerBuffer[i]);
            }

            center = (min + max) * 0.5f;
            center.y = 8f;
            halfExtents = new Vector3(
                Mathf.Max(0.5f, (max.x - min.x) * 0.5f + 1f),
                40f,
                Mathf.Max(0.5f, (max.z - min.z) * 0.5f + 1f));
            return true;
        }

        private void TryAddGroundPoint(Vector2 screenPoint)
        {
            if (TryScreenPointToGround(screenPoint, out Vector3 point))
                _dragGroundCornerBuffer.Add(point);
        }

        private bool TryScreenPointToGround(Vector2 screenPoint, out Vector3 point)
        {
            point = Vector3.zero;

            if (_camera == null)
                _camera = GetComponent<UnityEngine.Camera>();

            if (_camera == null)
                return false;

            Ray ray = _camera.ScreenPointToRay(screenPoint);

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _cubeMask))
                return false;

            point = hit.point;
            return true;
        }

        private void EndSelection()
        {
            if (!_isSelectionPressActive)
                return;

            _isSelectionPressActive = false;

            if (!_isSelectionDragActive)
            {
                if (HandleClickSelection())
                    _lastHandledLeftReleaseFrame = Time.frameCount;

                return;
            }

            _dragCurrentScreenPoint = Mouse.current.position.ReadValue();
            ApplyScreenDragSelection(GetDragScreenRect());
            HideSelectionVisual();
            _isSelectionDragActive = false;
            _lastHandledLeftReleaseFrame = Time.frameCount;
        }

        /// <summary>Викидає RaiseUnitDeselected для кожного вибраного юніта та очищає список виділення.</summary>
        private void DeselectAll(bool publish = true)
        {
            foreach (GameObject selection in _selections)
            {
                if (selection == null)
                    continue;

                if (IsBuildingSelection(selection))
                    EventManager.RaiseBuildingDeselected(selection);
                else if (IsUnitSelection(selection))
                    EventManager.RaiseUnitDeselected(selection);
            }

            _selections.Clear();

            if (publish)
                PublishSelectionChanged();
        }

        private bool TryAddSelection(GameObject selection, bool publish = true)
        {
            if (!TryResolveSelectionRoot(selection, out GameObject root, out SelectionKind kind) ||
                _selections.Contains(root))
            {
                return false;
            }

            _selections.Add(root);

            if (kind == SelectionKind.Building)
            {
                EnsureBuildingSelectionState(root);
                EventManager.RaiseBuildingSelected(root);
            }
            else
            {
                EventManager.RaiseUnitSelected(root);
            }

            if (publish)
                PublishSelectionChanged();

            return true;
        }

        private bool TryRemoveSelection(GameObject selection, bool publish = true)
        {
            if (!TryResolveSelectionRoot(selection, out GameObject root, out SelectionKind kind))
                return false;

            if (!_selections.Remove(root))
                return false;

            if (kind == SelectionKind.Building)
                EventManager.RaiseBuildingDeselected(root);
            else if (kind == SelectionKind.Unit)
                EventManager.RaiseUnitDeselected(root);

            if (publish)
                PublishSelectionChanged();

            return true;
        }

        private bool TryToggleSelection(GameObject selection)
        {
            if (!TryResolveSelectionRoot(selection, out GameObject root, out _))
                return false;

            return _selections.Contains(root)
                ? TryRemoveSelection(root)
                : TryAddSelection(root);
        }

        private void RemoveDeadSelections()
        {
            for (int i = _selections.Count - 1; i >= 0; i--)
            {
                if (_selections[i] == null)
                    _selections.RemoveAt(i);
            }
        }

        private bool HandleClickSelection()
        {
            if (!TryCreateMouseRay(out Ray ray))
                return false;

            if (TryFindClickedUnit(ray, out GameObject unit))
            {
                DeselectAll(false);
                TryAddSelection(unit, false);
                PublishSelectionChanged();
                return true;
            }

            if (TryFindClickedBuilding(ray, out GameObject building))
            {
                if (_isCtrlSelectionPress)
                {
                    TryToggleSelection(building);
                    return true;
                }

                DeselectAll(false);
                TryAddSelection(building, false);
                PublishSelectionChanged();
                return true;
            }

            if (!_isCtrlSelectionPress)
                DeselectAll();

            return false;
        }

        private bool TryFindClickedUnit(Ray ray, out GameObject unit)
        {
            unit = null;
            int unitMask = LayerMask.GetMask("PlayerUnit");

            if (unitMask == 0 || !Physics.Raycast(ray, out RaycastHit hit, 1000f, unitMask))
                return false;

            return TryResolveUnitRoot(hit.collider, out unit);
        }

        private bool TryFindClickedBuilding(Ray ray, out GameObject building)
        {
            building = null;
            int buildingMask = LayerMask.GetMask("Building");

            if (buildingMask == 0 ||
                !Physics.Raycast(ray, out RaycastHit hit, 1000f, buildingMask, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            return TryResolveBuildingRoot(hit.collider, out building);
        }

        private void CollectDragSelection(Collider[] hits)
        {
            CollectDragSelection(hits, default, false);
        }

        private void CollectDragSelection(Collider[] hits, Rect screenRect, bool useScreenRect)
        {
            _dragUnitBuffer.Clear();
            _dragBuildingBuffer.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                    continue;

                if (useScreenRect && !IsColliderInsideScreenRect(hit, screenRect))
                    continue;

                if (TryResolveUnitRoot(hit, out GameObject unit))
                {
                    AddUnique(_dragUnitBuffer, unit);
                    continue;
                }

                if (TryResolveBuildingRoot(hit, out GameObject building))
                    AddUnique(_dragBuildingBuffer, building);
            }
        }

        private void ApplyDragSelection(Collider[] hits)
        {
            DeselectAll(false);
            CollectDragSelection(hits);

            List<GameObject> selectedBuffer = _dragUnitBuffer.Count > 0
                ? _dragUnitBuffer
                : _dragBuildingBuffer;

            for (int i = 0; i < selectedBuffer.Count; i++)
                TryAddSelection(selectedBuffer[i], false);

            PublishSelectionChanged();
        }

        private void ApplyScreenDragSelection(Rect screenRect)
        {
            DeselectAll(false);

            if (!TryBuildDragWorldBroadphase(screenRect, out Vector3 center, out Vector3 halfExtents))
            {
                PublishSelectionChanged();
                return;
            }

            Collider[] hits = Physics.OverlapBox(
                center,
                halfExtents,
                Quaternion.identity,
                GetSelectableLayerMask(),
                QueryTriggerInteraction.Collide);

            CollectDragSelection(hits, screenRect, true);

            List<GameObject> selectedBuffer = _dragUnitBuffer.Count > 0
                ? _dragUnitBuffer
                : _dragBuildingBuffer;

            for (int i = 0; i < selectedBuffer.Count; i++)
                TryAddSelection(selectedBuffer[i], false);

            PublishSelectionChanged();
        }

        private bool IsColliderInsideScreenRect(Collider hit, Rect screenRect)
        {
            if (_camera == null)
                _camera = GetComponent<UnityEngine.Camera>();

            if (_camera == null || hit == null)
                return false;

            Bounds bounds = hit.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            return IsWorldPointInsideScreenRect(bounds.center, screenRect) ||
                   IsWorldPointInsideScreenRect(new Vector3(min.x, min.y, min.z), screenRect) ||
                   IsWorldPointInsideScreenRect(new Vector3(min.x, min.y, max.z), screenRect) ||
                   IsWorldPointInsideScreenRect(new Vector3(min.x, max.y, min.z), screenRect) ||
                   IsWorldPointInsideScreenRect(new Vector3(min.x, max.y, max.z), screenRect) ||
                   IsWorldPointInsideScreenRect(new Vector3(max.x, min.y, min.z), screenRect) ||
                   IsWorldPointInsideScreenRect(new Vector3(max.x, min.y, max.z), screenRect) ||
                   IsWorldPointInsideScreenRect(new Vector3(max.x, max.y, min.z), screenRect) ||
                   IsWorldPointInsideScreenRect(new Vector3(max.x, max.y, max.z), screenRect);
        }

        private bool IsWorldPointInsideScreenRect(Vector3 worldPoint, Rect screenRect)
        {
            Vector3 screenPoint = _camera.WorldToScreenPoint(worldPoint);
            return screenPoint.z > 0f && screenRect.Contains((Vector2)screenPoint);
        }

        private static void AddUnique(List<GameObject> target, GameObject value)
        {
            if (value != null && !target.Contains(value))
                target.Add(value);
        }

        private int GetSelectableLayerMask()
        {
            return _selectedLayerMask.value |
                   LayerMask.GetMask("PlayerUnit") |
                   LayerMask.GetMask("Building");
        }

        private static bool TryResolveSelectionRoot(
            GameObject selection,
            out GameObject root,
            out SelectionKind kind)
        {
            root = null;
            kind = SelectionKind.None;

            if (selection == null || selection.CompareTag("Enemy"))
                return false;

            if (TryResolveUnitRoot(selection, out root))
            {
                kind = SelectionKind.Unit;
                return true;
            }

            if (TryResolveBuildingRoot(selection, out root))
            {
                kind = SelectionKind.Building;
                return true;
            }

            return false;
        }

        private static bool TryResolveUnitRoot(Collider collider, out GameObject unit)
        {
            unit = null;

            if (collider == null)
                return false;

            return TryResolveUnitRoot(collider.gameObject, out unit);
        }

        private static bool TryResolveUnitRoot(GameObject selection, out GameObject unit)
        {
            unit = null;

            if (selection == null || selection.CompareTag("Enemy"))
                return false;

            if (selection.GetComponentInParent<BuildingProduction>() != null ||
                selection.GetComponentInParent<ConstructionCenter>() != null ||
                selection.GetComponentInParent<Outpost>() != null)
            {
                return false;
            }

            NavMeshAgent agent = selection.GetComponentInParent<NavMeshAgent>();
            UnitCombat combat = selection.GetComponentInParent<UnitCombat>();
            UnitSelectionState state = selection.GetComponentInParent<UnitSelectionState>();
            GameObject candidate =
                agent != null ? agent.gameObject :
                combat != null ? combat.gameObject :
                state != null ? state.gameObject :
                selection.layer == LayerMask.NameToLayer("PlayerUnit") ? selection : null;

            if (candidate == null || !BelongsToPlayer(candidate))
                return false;

            unit = candidate;
            return true;
        }

        private static bool TryResolveBuildingRoot(Collider collider, out GameObject building)
        {
            building = null;

            if (collider == null)
                return false;

            return TryResolveBuildingRoot(collider.gameObject, out building);
        }

        private static bool TryResolveBuildingRoot(GameObject selection, out GameObject building)
        {
            building = null;

            if (selection == null || selection.GetComponentInParent<Outpost>() != null)
                return false;

            BuildingProduction factory = selection.GetComponentInParent<BuildingProduction>();
            if (factory != null)
            {
                if (!BelongsToPlayer(factory.gameObject))
                    return false;

                building = factory.gameObject;
                return true;
            }

            ConstructionCenter constructionCenter = selection.GetComponentInParent<ConstructionCenter>();
            if (constructionCenter != null)
            {
                if (!BelongsToPlayer(constructionCenter.gameObject))
                    return false;

                building = constructionCenter.gameObject;
                return true;
            }

            return false;
        }

        private static bool IsUnitSelection(GameObject selection)
        {
            return TryResolveUnitRoot(selection, out _);
        }

        private static bool IsBuildingSelection(GameObject selection)
        {
            return TryResolveBuildingRoot(selection, out _);
        }

        private static bool BelongsToPlayer(GameObject selection)
        {
            TeamComponent team = selection != null ? selection.GetComponentInParent<TeamComponent>() : null;
            return team == null || LocalPlayerContext.IsLocalTeam(team.Team);
        }

        private static void EnsureBuildingSelectionState(GameObject building)
        {
            if (building != null && building.GetComponent<BuildingSelectionState>() == null)
                building.AddComponent<BuildingSelectionState>();
        }

        private void PublishSelectionChanged()
        {
            RemoveDeadSelections();
            PublishSelectionContext();
            EventManager.RaiseSelectionChanged(_selections);
        }

        private void RemoveDestroyedSelection(GameObject destroyed)
        {
            if (destroyed == null || _selections == null || !_selections.Remove(destroyed))
                return;

            PublishSelectionChanged();
        }

        private void PublishSelectionContext()
        {
            BuildingProduction factory = GetFirstSelectedFactory();
            if (factory != null)
            {
                SelectionManager.SetSelectedFactory(factory);
                EventManager.RaiseFactorySelected(factory);
                EventManager.RaiseOpenPanel(PanelType.Factory);
                return;
            }

            ConstructionCenter constructionCenter = GetFirstSelectedConstructionCenter();
            if (constructionCenter != null && GetSelectedUnitCountInternal() == 0)
            {
                SelectionManager.SetSelectedFactory(null);
                EventManager.RaiseConstructionCenterSelected(constructionCenter);
                EventManager.RaiseOpenPanel(PanelType.Construction);
                return;
            }

            SelectionManager.SetSelectedFactory(null);
            EventManager.RaiseConstructionClosed();
            EventManager.RaiseOpenPanel(PanelType.MainMenu);
        }

        private BuildingProduction GetFirstSelectedFactory()
        {
            for (int i = 0; i < _selections.Count; i++)
            {
                GameObject selection = _selections[i];
                if (selection == null)
                    continue;

                BuildingProduction factory = selection.GetComponent<BuildingProduction>();
                if (factory != null)
                    return factory;
            }

            return null;
        }

        private ConstructionCenter GetFirstSelectedConstructionCenter()
        {
            for (int i = 0; i < _selections.Count; i++)
            {
                GameObject selection = _selections[i];
                if (selection == null)
                    continue;

                ConstructionCenter constructionCenter = selection.GetComponent<ConstructionCenter>();
                if (constructionCenter != null)
                    return constructionCenter;
            }

            return null;
        }

        private int GetSelectedUnitCountInternal()
        {
            int count = 0;

            for (int i = 0; i < _selections.Count; i++)
            {
                if (IsUnitSelection(_selections[i]))
                    count++;
            }

            return count;
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

        private static bool IsCtrlPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }
    }
}
