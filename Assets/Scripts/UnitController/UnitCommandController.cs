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

            Vector3 destination = index == 0
                ? targetPoint
                : GetChessFormationPosition(targetPoint, index, _formationSpacing, rotation);

            agent.SetDestination(destination);

            EventManager.OnUnitMoveCommand?.Invoke(selection, destination);

            index++;
        }
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
