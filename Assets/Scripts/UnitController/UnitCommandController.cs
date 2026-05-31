using System.Collections.Generic;
using Strategy.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Strategy.Units
{
    /// <summary>
    /// Handles all player input for unit selection and ordering.
    /// Left-click drag performs a box-cast multi-select; right-click on an enemy issues an attack command;
    /// right-click on the ground issues a move command with chess-pattern formation offsets.
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
        [SerializeField] private float _selectionDragThreshold = 0.35f;

        private UnityEngine.Camera _camera;
        private GameObject _currentSelection;
        private Vector3 _startPoint;
        private bool _isSelectionPressActive;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _selections ??= new List<GameObject>();
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

        /// <summary>Raycasts the right-click position against enemy and ground layers, dispatching an attack or move command accordingly.</summary>
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

        /// <summary>Sets the manual attack target on each selected unit's UnitCombat and fires OnUnitAttackTargetChanged.</summary>
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
        /// Orders all selected units to move to chess-pattern formation positions around targetPoint,
        /// resolving each destination to a reachable NavMesh point before calling SetDestination.
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
            int index = 0;

            foreach (GameObject selection in _selections)
            {
                if (selection == null)
                    continue;

                NavMeshAgent agent = selection.GetComponent<NavMeshAgent>();

                if (agent == null)
                    continue;

                int formationIndex = index++;
                Vector3 destination = formationIndex == 0
                    ? targetPoint
                    : GetChessFormationPosition(targetPoint, formationIndex, _formationSpacing, rotation);

                if (!TryResolveNavMeshDestination(agent, destination, out destination))
                    continue;

                if (!agent.SetDestination(destination))
                    continue;

                EventManager.RaiseUnitMoveCommand(selection, destination);
            }
        }

        /// <summary>
        /// Samples the NavMesh near requestedDestination (with fallback radius) and validates path reachability.
        /// Falls back to the last reachable corner when only a partial path exists.
        /// </summary>
        private bool TryResolveNavMeshDestination(
            NavMeshAgent agent,
            Vector3 requestedDestination,
            out Vector3 resolvedDestination)
        {
            resolvedDestination = requestedDestination;

            if (agent == null || !agent.enabled || !TryEnsureAgentOnNavMesh(agent))
                return false;

            if (!TrySampleDestination(agent, requestedDestination, _navMeshSampleRadius, out NavMeshHit navHit) &&
                !TrySampleDestination(agent, requestedDestination, _navMeshFallbackSampleRadius, out navHit))
                return false;

            resolvedDestination = navHit.position;
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

            resolvedDestination = reachablePoint;
            return true;
        }

        /// <summary>Wraps NavMesh.SamplePosition restricted to the agent's areaMask with a clamped minimum radius.</summary>
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

        /// <summary>Warps the agent to the nearest NavMesh point if it has somehow moved off the mesh; returns false if recovery fails.</summary>
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

        /// <summary>Computes an alternating left/right, receding-row formation offset for unit at index relative to the move center point.</summary>
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

        /// <summary>Deselects all units and records the ground-hit start point for a potential drag-selection rectangle.</summary>
        private void StartSelectionPress()
        {
            if (IsPointerOverUi() || !RaycastToGround(out Vector3 hitPoint))
                return;

            DeselectAll();
            _startPoint = hitPoint;
            _isSelectionPressActive = true;
        }

        /// <summary>Initiates the drag-selection cube once movement exceeds the threshold, then resizes it to follow the cursor.</summary>
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

        /// <summary>Spawns the visual selection-rectangle cube prefab at the drag start position.</summary>
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

        /// <summary>Repositions and rescales the selection cube to span from the drag start point to the current cursor position.</summary>
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

        /// <summary>Uses OverlapBox with the selection cube's bounds to find player units inside and adds them to _selections via RaiseUnitSelected.</summary>
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

        /// <summary>Fires RaiseUnitDeselected for every currently selected unit and clears the selection list.</summary>
        private void DeselectAll()
        {
            foreach (GameObject selection in _selections)
            {
                if (selection != null)
                    EventManager.RaiseUnitDeselected(selection);
            }

            _selections.Clear();
        }

        /// <summary>Casts a ray from the camera through the mouse cursor against the ground/cube layer mask; returns the hit point.</summary>
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

        /// <summary>Creates a world-space ray from the camera through the current mouse position; returns false if the camera is unavailable.</summary>
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
