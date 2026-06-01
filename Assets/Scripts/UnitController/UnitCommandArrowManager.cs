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
    /// Малює двоточковий LineRenderer від кожного вибраного юніта до його поточного пункту призначення:
    /// зелений для цілі переміщення, червоний для цілі атаки. Лінії зберігаються, поки юніт вибраний;
    /// остання команда запам'ятовується, щоб лінія з'являлась при повторному виборі юніта.
    /// </summary>
    public class UnitCommandArrowManager : MonoBehaviour
    {
      [Header("Line Settings")]
      [SerializeField] private Material _moveLineMaterial;  // Зелена стрілка для команд переміщення на землю.
      [SerializeField] private Material _attackLineMaterial; // Червона стрілка для команд атаки ворога.

      [Header("Visual Settings")]
      [SerializeField] private float _lineWidth  = 0.15f;   // Товщина лінії у світових одиницях.
      [SerializeField] private float _heightOffset = 0.2f;   // Висота над землею для обох кінців лінії.

      private readonly Dictionary<GameObject, LineRenderer> _activeLines = new(); // Активні лінійні рендерери з ключем юніта.
      private readonly Dictionary<GameObject, Vector3> _moveTargets = new(); // Останній пункт призначення переміщення для кожного юніта (зберігається після зняття виділення для повторного показу при перевиборі).
      private readonly Dictionary<GameObject, Transform> _attackTargets = new(); // Останній трансформ цілі атаки для кожного юніта.
      private readonly List<GameObject> _unitsToRemove = new();  // Тимчасовий список для безпечного видалення знищених юнітів у циклі оновлення.
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
      /// Записує пункт призначення переміщення, очищає будь-яку ціль атаки, та малює зелену лінію до
      /// пункту призначення, якщо юніт зараз вибраний.
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
      /// Записує ціль атаки, очищає будь-який пункт призначення переміщення, та малює червону лінію до цілі,
      /// якщо юніт зараз вибраний.
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
      /// Викликається щокадру. Оновлює кінцеві точки активних ліній для живих вибраних юнітів, видаляє застарілі
      /// записи для знищених юнітів або нульових лінійних рендерерів, та приховує лінії для невибраних юнітів.
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

          // Якщо юніт знищений — прибираємо його лінію та дані.
          if (unit == null)
          {
            if (line != null)
            {
              Destroy(line.gameObject);
            }

            _unitsToRemove.Add(unit);
            continue;
          }
          // Якщо LineRenderer знищений ззовні — видаляємо застарілий запис.
          if (line == null)
          {
            _unitsToRemove.Add(unit);
            continue;
          }
          // Лінії повинні існувати лише поки юніт вибраний.
          if (!IsUnitSelected(unit))
          {
            HideLineOnly(unit);
            continue;
          }

          if (_attackTargets.TryGetValue(unit, out Transform attackTarget)) // Оновлення червоної лінії атаки.
          {
            if (attackTarget == null)
            {
              _attackTargets.Remove(unit);
              HideLineOnly(unit);
              continue;
            }
            UpdateLine(unit, line, attackTarget.position);
          }
          else if (_moveTargets.TryGetValue(unit, out Vector3 moveTarget))  // Оновлення зеленої лінії переміщення.
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
      /// Відновлює правильну лінію при повторному виборі юніта: червону, якщо є ціль атаки,
      /// зелену, якщо є пункт призначення переміщення.
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
      /// Знищує лінійний рендерер для юніта, але навмисно зберігає словники цілей переміщення/атаки,
      /// щоб лінію можна було відтворити при повторному виборі юніта.
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
      /// Повертає існуючий LineRenderer для юніта або створює новий під runtime-контейнером
      /// "Command Lines" із налаштованими параметрами ширини.
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
      /// Встановлює дві позиції LineRenderer від поточної позиції юніта до targetPosition,
      /// обидві підняті на _heightOffset.
      /// </summary>
      private void UpdateLine(GameObject unit, LineRenderer line, Vector3 targetPosition)
      {
        Vector3 start = unit.transform.position + Vector3.up * _heightOffset;
        Vector3 end = targetPosition + Vector3.up * _heightOffset;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
      }

      /// <summary>
      /// Видаляє весь стан (лінійний рендерер, ціль переміщення, ціль атаки) для заданого юніта,
      /// зазвичай викликається, коли знищення юніта підтверджено.
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
      /// Повертає true, коли компонент UnitSelectionState заданого юніта повідомляє IsSelected = true.
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
