using System.Collections.Generic;
using UnitController;
using UnityEngine;

public class UnitCommandArrowManager : MonoBehaviour
{
  [Header("Line Settings")]
  [SerializeField] private Material _moveLineMaterial;  // Зелена стрілка для руху по місцевості
  [SerializeField] private Material _attackLineMaterial; // Червона стрілка для атаки ворога
  
  [Header("Visual Settings")]
  [SerializeField] private float _lineWidth  = 0.15f;   // Товщина лінії.
  [SerializeField] private float _heightOffset = 0.2f;   // Підняття лінії над землею.
  
  private readonly Dictionary<GameObject, LineRenderer> _activeLines = new(); // Активні лінії для вибраних юнітів.
  private readonly Dictionary<GameObject, Vector3> _moveTargets = new(); // Останні точки руху юнітів. НЕ видаляємо при deselect, щоб при повторному виборі лінія повернулась
  private readonly Dictionary<GameObject, Transform> _attackTargets = new(); //Останні цілі атаки юнітів
  private readonly List<GameObject> _unitsToRemove = new();  // Тимчасовий список для безпечного видалення мертвих/null юнітів.
  

  private void OnEnable()
  {
    EventManager.OnUnitMoveCommand += ShowMoveLine;
    EventManager.OnUnitAttackTargetChanged += ShowAttackLine;
    EventManager.OnUnitSelected += ShowLastCommandForUnit;
    EventManager.OnUnitDeselected += HideLineOnly;
  }

  private void OnDisable()
  {
    EventManager.OnUnitSelected -= ShowLastCommandForUnit;
    EventManager.OnUnitDeselected -= HideLineOnly;
    EventManager.OnUnitMoveCommand -= ShowMoveLine;
    EventManager.OnUnitAttackTargetChanged -= ShowAttackLine;
  }

  private void Update()
  {
    UpdateLines();
  }

  private void ShowMoveLine(GameObject unit, Vector3 targetPosition)
  {
    if (unit == null)
      return;

    _moveTargets[unit] = targetPosition;
    _attackTargets.Remove(unit);
    
    if (!IsUnitSelected(unit))
      return;

    LineRenderer line = GetOrCreateLine(unit);
    line.material = _moveLineMaterial;

    UpdateLine(unit, line, targetPosition);
  }

  
  private void ShowAttackLine(GameObject unit, Transform target)
  {
    if (unit == null || target == null)
      return;
    
    _attackTargets[unit] = target;
    _moveTargets.Remove(unit);
    
    if (!IsUnitSelected(unit))
      return;
    
    LineRenderer line = GetOrCreateLine(unit);
    line.material = _attackLineMaterial;

    UpdateLine(unit, line, target.position);
  }
  
  private void UpdateLines()
  {
    _unitsToRemove.Clear();
    
    foreach (var pair in _activeLines)
    {
      GameObject unit = pair.Key;
      LineRenderer line = pair.Value;
      
// Якщо юніт знищений — треба видалити його лінію і дані
      if (unit == null)
      {
        if (line != null)
        {
          Destroy(line.gameObject);
        }
        
        _unitsToRemove.Add(unit);
        continue;
      }
      // Якщо лінія знищена — чистимо запис.
      if (line == null)
      {
        _unitsToRemove.Add(unit);
        continue;
      }
// Якщо юніт не вибраний — лінії бути не повинно.
      if (!IsUnitSelected(unit))
      {
        HideLineOnly(unit);
        continue;
      }

      if (_attackTargets.TryGetValue(unit, out Transform attackTarget)) // Червона лінія до ворога.
      {
        if (attackTarget == null)
        {
          _attackTargets.Remove(unit);
          HideLineOnly(unit);
          continue;
        }
        UpdateLine(unit, line, attackTarget.position);
      }
      else if (_moveTargets.TryGetValue(unit, out Vector3 moveTarget))  // Зелена лінія до точки руху
      {
        UpdateLine(unit, line, moveTarget);
      }
    }
    
    foreach (GameObject unit in _unitsToRemove)
    {
      ClearUnit(unit);
    }
  }
  
  private void ShowLastCommandForUnit(GameObject unit)
  {
    if (unit == null)
      return;

    // Якщо юніт атакує — показуємо червону лінію.
    if (_attackTargets.TryGetValue(unit, out Transform attackTarget))
    {
      if (attackTarget == null)
      {
        _attackTargets.Remove(unit);
        return;
      }

      LineRenderer line = GetOrCreateLine(unit);
      line.material = _attackLineMaterial;
      UpdateLine(unit, line, attackTarget.position);
      return;
    }

    // Якщо юніт рухається — показуємо зелену лінію.
    if (_moveTargets.TryGetValue(unit, out Vector3 moveTarget))
    {
      LineRenderer line = GetOrCreateLine(unit);
      line.material = _moveLineMaterial;
      UpdateLine(unit, line, moveTarget);
    }
  }
  
  private void HideLineOnly(GameObject unit)
  {
    if (unit == null)
      return;

    if (_activeLines.TryGetValue(unit, out LineRenderer line))
    {
      if (line != null)
        Destroy(line.gameObject);

      _activeLines.Remove(unit);
    }

    // ВАЖЛИВО:
    // Тут НЕ видаляємо _moveTargets і _attackTargets.
    // Інакше при повторному виборі зелена/червона лінія не повернеться.
  }

  private LineRenderer GetOrCreateLine(GameObject unit)
  {
    if (_activeLines.TryGetValue(unit, out LineRenderer existingLine))
      return existingLine;

    GameObject lineObject = new GameObject($"CommandLine_{unit.name}");

    LineRenderer line = lineObject.AddComponent<LineRenderer>();

    line.positionCount = 2;
    line.useWorldSpace = true;

    line.startWidth = _lineWidth;
    line.endWidth = _lineWidth;

    line.numCapVertices = 4;
    line.numCornerVertices = 4;

    _activeLines[unit] = line;

    return line;
  }
  
  private void UpdateLine(GameObject unit, LineRenderer line, Vector3 targetPosition)
  {
    Vector3 start = unit.transform.position + Vector3.up * _heightOffset;
    Vector3 end = targetPosition + Vector3.up * _heightOffset;

    line.SetPosition(0, start);
    line.SetPosition(1, end);
  }
  
  private void ClearUnit(GameObject unit)
  {
    if (unit != null && _activeLines.TryGetValue(unit, out LineRenderer line))
    {
      if (line != null)
        Destroy(line.gameObject);
    }

    _activeLines.Remove(unit);
    _moveTargets.Remove(unit);
    _attackTargets.Remove(unit);
  }

  private bool IsUnitSelected(GameObject unit)
  {
    if (unit == null)
      return false;

    UnitSelectionState state = unit.GetComponent<UnitSelectionState>();

    return state != null && state.IsSelected;
  }
}
