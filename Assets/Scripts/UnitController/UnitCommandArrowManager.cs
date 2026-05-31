using System.Collections.Generic;
using Strategy.Units;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Draws a two-point LineRenderer from each selected unit to its current command destination:
    /// green for a move target, red for an attack target. Lines persist as long as the unit is
    /// selected; the last command is remembered so the line reappears when the unit is reselected.
    /// </summary>
    public class UnitCommandArrowManager : MonoBehaviour
    {
      [Header("Line Settings")]
      [SerializeField] private Material _moveLineMaterial;  // Green arrow for move-to-ground commands.
      [SerializeField] private Material _attackLineMaterial; // Red arrow for attack-enemy commands.

      [Header("Visual Settings")]
      [SerializeField] private float _lineWidth  = 0.15f;   // Line thickness in world units.
      [SerializeField] private float _heightOffset = 0.2f;   // Height above ground for both endpoints.

      private readonly Dictionary<GameObject, LineRenderer> _activeLines = new(); // Active line renderers keyed by unit.
      private readonly Dictionary<GameObject, Vector3> _moveTargets = new(); // Last move destination per unit (kept after deselect for re-show on reselect).
      private readonly Dictionary<GameObject, Transform> _attackTargets = new(); // Last attack target transform per unit.
      private readonly List<GameObject> _unitsToRemove = new();  // Temporary list for safe removal of destroyed units inside the update loop.
      private readonly List<GameObject> _activeLineUnits = new();


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

      /// <summary>
      /// Records the move destination, clears any attack target, and draws a green line to the
      /// destination if the unit is currently selected.
      /// </summary>
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

      /// <summary>
      /// Records the attack target, clears any move target, and draws a red line to the target
      /// if the unit is currently selected.
      /// </summary>
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

      /// <summary>
      /// Called every frame. Updates active line endpoints for living selected units, removes stale
      /// entries for destroyed units or null line renderers, and hides lines for deselected units.
      /// </summary>
      private void UpdateLines()
      {
        _unitsToRemove.Clear();

        _activeLineUnits.Clear();

        foreach (GameObject activeUnit in _activeLines.Keys)
          _activeLineUnits.Add(activeUnit);

        foreach (GameObject unit in _activeLineUnits)
        {
          _activeLines.TryGetValue(unit, out LineRenderer line);

          // If the unit has been destroyed, clean up its line and data.
          if (unit == null)
          {
            if (line != null)
            {
              Destroy(line.gameObject);
            }

            _unitsToRemove.Add(unit);
            continue;
          }
          // If the line renderer was destroyed externally, remove the stale entry.
          if (line == null)
          {
            _unitsToRemove.Add(unit);
            continue;
          }
          // Lines should only exist while the unit is selected.
          if (!IsUnitSelected(unit))
          {
            HideLineOnly(unit);
            continue;
          }

          if (_attackTargets.TryGetValue(unit, out Transform attackTarget)) // Update red attack line.
          {
            if (attackTarget == null)
            {
              _attackTargets.Remove(unit);
              HideLineOnly(unit);
              continue;
            }
            UpdateLine(unit, line, attackTarget.position);
          }
          else if (_moveTargets.TryGetValue(unit, out Vector3 moveTarget))  // Update green move line.
          {
            UpdateLine(unit, line, moveTarget);
          }
        }

        foreach (GameObject unit in _unitsToRemove)
        {
          ClearUnit(unit);
        }
      }

      /// <summary>
      /// Restores the correct line when a unit is reselected: red if it has an attack target,
      /// green if it has a pending move destination.
      /// </summary>
      private void ShowLastCommandForUnit(GameObject unit)
      {
        if (unit == null)
          return;

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

        if (_moveTargets.TryGetValue(unit, out Vector3 moveTarget))
        {
          LineRenderer line = GetOrCreateLine(unit);
          line.material = _moveLineMaterial;
          UpdateLine(unit, line, moveTarget);
        }
      }

      /// <summary>
      /// Destroys the line renderer for the unit but intentionally preserves the move/attack target
      /// dictionaries so the line can be recreated if the unit is reselected.
      /// </summary>
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
      }

      /// <summary>
      /// Returns the existing LineRenderer for the unit or creates a new one under the
      /// "Command Lines" runtime container with the configured width settings.
      /// </summary>
      private LineRenderer GetOrCreateLine(GameObject unit)
      {
        if (_activeLines.TryGetValue(unit, out LineRenderer existingLine))
          return existingLine;

        GameObject lineObject = new GameObject($"CommandLine_{unit.name}");
        lineObject.transform.SetParent(RuntimeObjectContainer.Get("Command Lines"), false);

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

      /// <summary>
      /// Sets the two LineRenderer positions from the unit's current world position to targetPosition,
      /// both raised by _heightOffset.
      /// </summary>
      private void UpdateLine(GameObject unit, LineRenderer line, Vector3 targetPosition)
      {
        Vector3 start = unit.transform.position + Vector3.up * _heightOffset;
        Vector3 end = targetPosition + Vector3.up * _heightOffset;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
      }

      /// <summary>
      /// Removes all state (line renderer, move target, attack target) for the given unit,
      /// typically called when the unit is confirmed destroyed.
      /// </summary>
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

      /// <summary>
      /// Returns true when the given unit's UnitSelectionState component reports IsSelected = true.
      /// </summary>
      private bool IsUnitSelected(GameObject unit)
      {
        if (unit == null)
          return false;

        UnitSelectionState state = unit.GetComponent<UnitSelectionState>();

        return state != null && state.IsSelected;
      }
    }
}
