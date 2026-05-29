using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class UnitCommandController : MonoBehaviour
{
    [SerializeField] private GameObject _cubePrefab;
    [SerializeField] private LayerMask _enemyMask;
    [SerializeField] private LayerMask _cubeMask;
    [SerializeField] private LayerMask _selectedLayerMask;
    [SerializeField] private List<GameObject> _selections;
    [SerializeField] private float _formationSpacing = 4f;
    [SerializeField] private float _navMeshSampleRadius = 3f;
    [SerializeField] private float _navMeshFallbackSampleRadius = 8f;
   
    private Camera _camera;
    private GameObject _currentSelection;

    private Vector3 _startPoint;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Update()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame && _selections.Count > 0)
            ControllerUnits();
        
        if (Mouse.current.leftButton.wasPressedThisFrame)
            StartSelection();

        if (Mouse.current.leftButton.isPressed && _currentSelection != null)
            UpdateSelection();

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            EndSelection();
    }

    private void ControllerUnits()
    {
        if (_selections == null || _selections.Count == 0)
            return;

        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit enemyHit, 1000f, _enemyMask))
        {
            CommandAttack(enemyHit.transform);
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, _cubeMask))
        {
            CommandMove(groundHit.point);
        }
    }
    
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

            EventManager.OnUnitAttackTargetChanged?.Invoke(selection, enemy);
        }
    }
    
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

            EventManager.OnUnitMoveCommand?.Invoke(selection, destination);
        }
    }

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

        agent.transform.position = hit.position;
        agent.Warp(hit.position);
        return agent.isOnNavMesh;
    }
    
    private Vector3 GetChessFormationPosition(
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

        Vector3 localOffset = new Vector3(x, 0f, z);

        return center + rotation * localOffset;
    }
    
    private GameObject GetFirstValidSelection()
    {
        foreach (var obj in _selections)
        {
            if (obj != null)
                return obj;
        }
        return null;
    }
    
    private void StartSelection()
    {
        if (!RaycastToGround(out var hitPoint)) return;
        
        foreach (GameObject selection in _selections)
        {
            if (selection == null) continue;
            
            EventManager.OnUnitDeselected?.Invoke(selection);
        }
        
        _selections.Clear();

        _startPoint = hitPoint;
        _currentSelection = Instantiate(_cubePrefab, new Vector3(_startPoint.x, 1f, _startPoint.z),
            Quaternion.identity, RuntimeObjectContainer.Get("Selection"));
    }

    private void UpdateSelection()
    {
        if (!RaycastToGround(out var currentPoint)) return;

        float x = (_startPoint.x - currentPoint.x) * -1f;
        float z = _startPoint.z - currentPoint.z;

        if (x < 0 && z < 0)
        {
            _currentSelection.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }
        else if (x < 0)
        {
            _currentSelection.transform.localRotation = Quaternion.Euler(0, 0, 180);
        }
        else if (z < 0)
        {
            _currentSelection.transform.localRotation = Quaternion.Euler(180, 0, 0);
        }
        else
        {
            _currentSelection.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        _currentSelection.transform.localScale = new Vector3(MathF.Abs(x), 1f, MathF.Abs(z));
    }

    private void EndSelection()
    {
        if (_currentSelection == null) return;

        RaycastHit[] hits = Physics.BoxCastAll(_currentSelection.transform.position,
            _currentSelection.transform.localScale, Vector3.up, Quaternion.identity, 0, _selectedLayerMask);

        foreach (RaycastHit hit in hits)
        {
            if(hit.collider.CompareTag("Enemy")) continue;
            
            _selections.Add(hit.transform.gameObject);
            EventManager.OnUnitSelected?.Invoke(hit.transform.gameObject);
        }

        Destroy(_currentSelection);
        _currentSelection = null;
    }

    private bool RaycastToGround(out Vector3 point)
    {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 1000, _cubeMask))
        {
            point = hit.point;
            return true;
        }

        point = Vector3.zero;
        return false;
    }
}
