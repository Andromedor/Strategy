using System.Collections.Generic;
using Strategy.Core;
using Strategy.Buildings;
using TMPro;
using Strategy.Units;
using UnityEngine;

using Strategy.Data;
using Strategy.UI;
namespace Strategy.UI
{
    public class SelectionInfoPanelUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _subtitleText;
        [SerializeField] private TMP_Text _statsText;
        [SerializeField] private Transform _compactListRoot;
        [SerializeField] private TMP_Text _compactListTextPrefab;

        private readonly List<GameObject> _selectedUnits = new();
        private readonly List<TMP_Text> _compactRows = new();
        private Object _selectedObject;
        private float _nextRefreshTime;

        private void OnEnable()
        {
            EventManager.OnUnitSelected += OnUnitSelected;
            EventManager.OnUnitDeselected += OnUnitDeselected;
            EventManager.OnFactorySelected += OnFactorySelected;
            EventManager.OnConstructionCenterSelected += OnConstructionCenterSelected;
            EventManager.OnOutpostSelected += OnOutpostSelected;
            EventManager.OnConstructionClosed += ShowIdle;
            ShowIdle();
        }

        private void OnDisable()
        {
            EventManager.OnUnitSelected -= OnUnitSelected;
            EventManager.OnUnitDeselected -= OnUnitDeselected;
            EventManager.OnFactorySelected -= OnFactorySelected;
            EventManager.OnConstructionCenterSelected -= OnConstructionCenterSelected;
            EventManager.OnOutpostSelected -= OnOutpostSelected;
            EventManager.OnConstructionClosed -= ShowIdle;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + 0.25f;

            if (_selectedUnits.Count > 0)
                RefreshUnits();
            else if (_selectedObject != null)
                RefreshObject();
        }

        private void OnUnitSelected(GameObject unit)
        {
            if (unit == null)
                return;

            _selectedObject = null;

            if (!_selectedUnits.Contains(unit))
                _selectedUnits.Add(unit);

            RefreshUnits();
        }

        private void OnUnitDeselected(GameObject unit)
        {
            if (unit != null)
                _selectedUnits.Remove(unit);

            if (_selectedUnits.Count > 0)
                RefreshUnits();
            else
                ShowIdle();
        }

        private void OnFactorySelected(BuildingProduction factory)
        {
            ClearUnits();
            _selectedObject = factory;
            RefreshObject();
        }

        private void OnConstructionCenterSelected(ConstructionCenter center)
        {
            ClearUnits();
            _selectedObject = center;
            RefreshObject();
        }

        private void OnOutpostSelected(Outpost outpost)
        {
            ClearUnits();
            _selectedObject = outpost;
            RefreshObject();
        }

        private void ShowIdle()
        {
            ClearUnits();
            _selectedObject = null;
            SetText("No selection", "Select units or buildings", "Orders and object data will appear here.");
            SetCompactRows(null);
        }

        private void RefreshUnits()
        {
            RemoveDeadUnits();

            if (_selectedUnits.Count == 0)
            {
                ShowIdle();
                return;
            }

            if (_selectedUnits.Count == 1)
            {
                GameObject unit = _selectedUnits[0];
                UnitCombat combat = unit.GetComponent<UnitCombat>();
                TeamComponent team = unit.GetComponent<TeamComponent>();
                string unitName = GetDisplayName(unit.name);
                string teamText = team != null ? team.Team.ToString() : "Unknown team";

                if (combat == null || combat.UnitData == null)
                {
                    SetText(unitName, teamText, "No combat data");
                    SetCompactRows(null);
                    return;
                }

                UnitData data = combat.UnitData;
                SetText(
                    unitName,
                    teamText + " Unit",
                    $"HP {FormatNumber(combat.CurrentHealth)} / {FormatNumber(combat.MaxHealth)}\n" +
                    $"Damage {FormatNumber(data.Damage)}   Range {FormatNumber(data.AttackRange)}\n" +
                    $"Attack {FormatNumber(data.AttackDelay)}s   Speed {FormatNumber(data.Speed)}");
                SetCompactRows(null);
                return;
            }

            SetText(
                _selectedUnits.Count + " units selected",
                "Group selection",
                "Right-click terrain to move. Right-click enemy to attack.");

            List<string> rows = new List<string>();
            for (int i = 0; i < _selectedUnits.Count && i < 8; i++)
                rows.Add(GetDisplayName(_selectedUnits[i].name));

            if (_selectedUnits.Count > 8)
                rows.Add("+" + (_selectedUnits.Count - 8) + " more");

            SetCompactRows(rows);
        }

        private void RefreshObject()
        {
            if (_selectedObject is BuildingProduction factory)
            {
                TeamComponent team = factory.GetComponent<TeamComponent>();
                int itemCount = factory.Items.Count;
                SetText(
                    GetDisplayName(factory.name),
                    (team != null ? team.Team.ToString() : "Player") + " production",
                    $"Queue available\nUnits: {itemCount}\nUse the Units tab to train vehicles.");
                SetCompactRows(null);
                return;
            }

            if (_selectedObject is ConstructionCenter center)
            {
                TeamComponent team = center.GetComponent<TeamComponent>();
                SetText(
                    GetDisplayName(center.name),
                    (team != null ? team.Team.ToString() : "Player") + " construction center",
                    $"Build radius {FormatNumber(center.BuildRadius)}\nUse the Build tab to place structures.");
                SetCompactRows(null);
                return;
            }

            if (_selectedObject is Outpost outpost)
            {
                string owner = outpost.Owner.HasValue ? outpost.Owner.Value.ToString() : "Neutral";
                string state = outpost.IsUpgraded ? "Upgraded" : "Standard";
                SetText(
                    GetDisplayName(outpost.name),
                    owner + " capture point",
                    $"{state}\nIncome {FormatNumber(outpost.CurrentResourcePerMinute)}/min\n" +
                    $"Capture {FormatNumber(outpost.CaptureProgress * 100f)}%");
                SetCompactRows(null);
                return;
            }

            ShowIdle();
        }

        private void ClearUnits()
        {
            _selectedUnits.Clear();
            SetCompactRows(null);
        }

        private void RemoveDeadUnits()
        {
            for (int i = _selectedUnits.Count - 1; i >= 0; i--)
            {
                if (_selectedUnits[i] == null)
                    _selectedUnits.RemoveAt(i);
            }
        }

        private void SetText(string title, string subtitle, string stats)
        {
            if (_titleText != null)
                _titleText.text = title;

            if (_subtitleText != null)
                _subtitleText.text = subtitle;

            if (_statsText != null)
                _statsText.text = stats;
        }

        private void SetCompactRows(List<string> rows)
        {
            if (_compactListRoot == null || _compactListTextPrefab == null)
                return;

            for (int i = 0; i < _compactRows.Count; i++)
            {
                if (_compactRows[i] != null)
                    Destroy(_compactRows[i].gameObject);
            }

            _compactRows.Clear();

            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                TMP_Text row = Instantiate(_compactListTextPrefab, _compactListRoot);
                row.text = rows[i];
                row.gameObject.SetActive(true);
                _compactRows.Add(row);
            }
        }

        private static string GetDisplayName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return "Unknown";

            return objectName
                .Replace("(Clone)", string.Empty)
                .Replace("_yup", string.Empty)
                .Replace("unit_", string.Empty)
                .Replace("struct_", string.Empty)
                .Replace("_", " ")
                .Trim();
        }

        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }
    }
}
