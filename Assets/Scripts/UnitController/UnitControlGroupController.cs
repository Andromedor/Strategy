using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Core;
using Strategy.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Strategy.Units
{
    /// <summary>
    /// Зберігає RTS control groups 1-9: Ctrl+число записує поточний вибір, число повертає групу.
    /// Компонент не керує підсвіткою напряму, а делегує перевиділення до UnitCommandController.
    /// </summary>
    public class UnitControlGroupController : MonoBehaviour
    {
        private const int GroupCount = 9;

        [SerializeField] private UnitCommandController _selectionController;

        private readonly List<GameObject>[] _groups = new List<GameObject>[GroupCount];
        private readonly List<GameObject> _selectionBuffer = new();
        private readonly List<GameObject> _recallBuffer = new();
        private readonly bool[] _groupKeyWasDown = new bool[GroupCount];
        private float _nextPruneTime;
        private bool _ctrlWasDown;

        private void Awake()
        {
            if (_selectionController == null)
                _selectionController = GetComponent<UnitCommandController>();

            for (int i = 0; i < _groups.Length; i++)
                _groups[i] = new List<GameObject>();
        }

        private void OnEnable()
        {
            EventManager.OnUnitDestroyed += RemoveUnitFromGroups;
            PublishAllGroups();
        }

        private void OnDisable()
        {
            EventManager.OnUnitDestroyed -= RemoveUnitFromGroups;
        }

        private void Update()
        {
            PruneDeadUnitsPeriodically();
        }

        private void LateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            bool ctrlPressed = IsCtrlPressed(keyboard);
            for (int groupNumber = 1; groupNumber <= GroupCount; groupNumber++)
            {
                int index = groupNumber - 1;
                bool keyDown = IsGroupKeyPressed(keyboard, groupNumber);
                bool keyPressed = WasGroupKeyPressed(keyboard, groupNumber) || (keyDown && !_groupKeyWasDown[index]);
                bool ctrlPressedWithHeldNumber = keyDown && ctrlPressed && !_ctrlWasDown;

                _groupKeyWasDown[index] = keyDown;

                if (!keyPressed && !ctrlPressedWithHeldNumber)
                    continue;

                ProcessGroupKey(groupNumber, ctrlPressed);
                _ctrlWasDown = ctrlPressed;
                return;
            }

            _ctrlWasDown = ctrlPressed;
        }

        public void ProcessGroupKey(int groupNumber, bool ctrlPressed)
        {
            if (ctrlPressed)
                SaveGroup(groupNumber);
            else
                RecallGroup(groupNumber);
        }

        public void SaveGroup(int groupNumber)
        {
            if (!TryGetGroup(groupNumber, out List<GameObject> group) || _selectionController == null)
                return;

            _selectionController.CopySelectedObjects(_selectionBuffer);
            group.Clear();

            for (int i = 0; i < _selectionBuffer.Count; i++)
            {
                GameObject selection = _selectionBuffer[i];

                if (selection != null && !group.Contains(selection))
                    group.Add(selection);
            }

            PublishGroup(groupNumber);
        }

        public void RecallGroup(int groupNumber)
        {
            if (!TryGetGroup(groupNumber, out List<GameObject> group) || _selectionController == null)
                return;

            PruneGroup(group);

            if (group.Count == 0)
            {
                PublishGroup(groupNumber);
                return;
            }

            _recallBuffer.Clear();
            _recallBuffer.AddRange(group);
            _selectionController.SelectObjects(_recallBuffer);
            PublishGroup(groupNumber);
        }

        public int GetGroupUnitCount(int groupNumber)
        {
            if (!TryGetGroup(groupNumber, out List<GameObject> group))
                return 0;

            PruneGroup(group);
            return group.Count;
        }

        private void RemoveUnitFromGroups(GameObject unit)
        {
            if (unit == null)
                return;

            for (int i = 0; i < _groups.Length; i++)
            {
                if (!_groups[i].Remove(unit))
                    continue;

                PublishGroup(i + 1);
            }
        }

        private void PruneDeadUnitsPeriodically()
        {
            if (Time.unscaledTime < _nextPruneTime)
                return;

            _nextPruneTime = Time.unscaledTime + 0.25f;

            for (int i = 0; i < _groups.Length; i++)
            {
                if (PruneGroup(_groups[i]))
                    PublishGroup(i + 1);
            }
        }

        private static bool PruneGroup(List<GameObject> group)
        {
            bool changed = false;

            for (int i = group.Count - 1; i >= 0; i--)
            {
                if (group[i] != null)
                    continue;

                group.RemoveAt(i);
                changed = true;
            }

            return changed;
        }

        private void PublishAllGroups()
        {
            for (int i = 1; i <= GroupCount; i++)
                PublishGroup(i);
        }

        private void PublishGroup(int groupNumber)
        {
            if (!TryGetGroup(groupNumber, out List<GameObject> group))
            {
                EventManager.RaiseControlGroupUpdated(groupNumber, 0);
                return;
            }

            PruneGroup(group);
            Sprite icon = null;
            string fallbackText = string.Empty;

            if (group.Count > 0)
                ResolveRepresentativeObject(group[0], out icon, out fallbackText);

            EventManager.RaiseControlGroupUpdated(groupNumber, group.Count, icon, fallbackText);
        }

        private bool TryGetGroup(int groupNumber, out List<GameObject> group)
        {
            if (groupNumber < 1 || groupNumber > GroupCount)
            {
                group = null;
                return false;
            }

            group = _groups[groupNumber - 1];
            return true;
        }

        private static bool IsCtrlPressed(Keyboard keyboard)
        {
            return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        }

        private static void ResolveRepresentativeObject(GameObject unit, out Sprite icon, out string fallbackText)
        {
            icon = null;
            fallbackText = string.Empty;

            if (unit == null)
                return;

            if (unit.GetComponent<BuildingProduction>() != null)
            {
                fallbackText = "FAC";
                return;
            }

            if (unit.GetComponent<ConstructionCenter>() != null)
            {
                fallbackText = "BASE";
                return;
            }

            UnitCombat combat = unit.GetComponent<UnitCombat>();
            UnitData data = combat != null ? combat.UnitData : null;
            string displayName = ResolveDisplayName(data, unit);

            if (data != null)
            {
                icon = data.SelectionIcon;

                if (!string.IsNullOrWhiteSpace(data.SelectionFallbackText))
                {
                    fallbackText = data.SelectionFallbackText;
                    return;
                }
            }

            fallbackText = BuildInitials(displayName);
        }

        private static string ResolveDisplayName(UnitData data, GameObject unit)
        {
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.name))
                    return data.name;
            }

            return unit != null ? unit.name : "Unit";
        }

        private static string BuildInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "UNIT";

            string[] words = displayName.Replace("_", " ").Split(' ');
            string result = string.Empty;

            for (int i = 0; i < words.Length && result.Length < 3; i++)
            {
                if (!string.IsNullOrWhiteSpace(words[i]))
                    result += char.ToUpperInvariant(words[i][0]);
            }

            if (!string.IsNullOrWhiteSpace(result))
                return result;

            return displayName.Length <= 3
                ? displayName.ToUpperInvariant()
                : displayName.Substring(0, 3).ToUpperInvariant();
        }

        private static bool IsGroupKeyPressed(Keyboard keyboard, int groupNumber)
        {
            switch (groupNumber)
            {
                case 1:
                    return keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed;
                case 2:
                    return keyboard.digit2Key.isPressed || keyboard.numpad2Key.isPressed;
                case 3:
                    return keyboard.digit3Key.isPressed || keyboard.numpad3Key.isPressed;
                case 4:
                    return keyboard.digit4Key.isPressed || keyboard.numpad4Key.isPressed;
                case 5:
                    return keyboard.digit5Key.isPressed || keyboard.numpad5Key.isPressed;
                case 6:
                    return keyboard.digit6Key.isPressed || keyboard.numpad6Key.isPressed;
                case 7:
                    return keyboard.digit7Key.isPressed || keyboard.numpad7Key.isPressed;
                case 8:
                    return keyboard.digit8Key.isPressed || keyboard.numpad8Key.isPressed;
                case 9:
                    return keyboard.digit9Key.isPressed || keyboard.numpad9Key.isPressed;
                default:
                    return false;
            }
        }

        private static bool WasGroupKeyPressed(Keyboard keyboard, int groupNumber)
        {
            switch (groupNumber)
            {
                case 1:
                    return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
                case 2:
                    return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
                case 3:
                    return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
                case 4:
                    return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
                case 5:
                    return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
                case 6:
                    return keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame;
                case 7:
                    return keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame;
                case 8:
                    return keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame;
                case 9:
                    return keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame;
                default:
                    return false;
            }
        }
    }
}
