using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class SelectController : MonoBehaviour
{
    [SerializeField] private GameObject _cubePrefab;
    [SerializeField] private LayerMask _cubeMask;
    [SerializeField] private LayerMask _selectedLayerMask;
    [SerializeField] private List<GameObject> _selections;
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

        if (!Physics.Raycast(ray, out RaycastHit agentTarget, 1000, _cubeMask))
            return;

        GameObject firstUnit = GetFirstValidSelection();

        if (firstUnit == null)
            return;

        Vector3 dir = (agentTarget.point - firstUnit.transform.position).normalized;
        Quaternion rot = Quaternion.LookRotation(dir);

        int index = 0;

        foreach (GameObject selection in _selections)
        {
            if (selection == null)
                continue;

            NavMeshAgent agent = selection.GetComponent<NavMeshAgent>();
            if (agent == null)
                continue;

            Vector3 pos = GetCirclePosition(
                agentTarget.point,
                index,
                _selections.Count,
                5f,
                rot
            );

            agent.SetDestination(pos);
            
            EventManager.OnUnitMoveCommand?.Invoke(selection, pos);
            
            index++;
        }
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
    
    private Vector3 GetCirclePosition(Vector3 center, int index, int count, float spacing, Quaternion rotation)
    {
        int rowSize = Mathf.CeilToInt(Mathf.Sqrt(count));

        int row = index / rowSize;
        int col = index % rowSize;

        float offsetX = (col - rowSize / 2f) * spacing;
        float offsetZ = (row - rowSize / 2f) * spacing;

        Vector3 localOffset = new Vector3(offsetX, 0, offsetZ);

        return center + rotation * localOffset;
    }

    private void StartSelection()
    {
        if (!RaycastToGround(out var hitPoint)) return;
        
        foreach (GameObject selection in _selections)
        {
            if (selection == null) continue;
            selection.transform.GetChild(0).gameObject.SetActive(false);
            EventManager.OnUnitDeselected?.Invoke(selection);
        }
        
        _selections.Clear();

        _startPoint = hitPoint;
        _currentSelection = Instantiate(_cubePrefab, new Vector3(_startPoint.x, 1f, _startPoint.z),
            Quaternion.identity);
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
            hit.transform.GetChild(0).gameObject.SetActive(true);
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