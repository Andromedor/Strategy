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
    /// <summary>
    /// Нижня HUD-панель, що показує контекстну інформацію про поточний вибір.
    /// Обробляє вибір одного/кількох юнітів, заводів, будівельних центрів та аванпостів,
    /// опитуючи живі дані з частотою ~4 Гц для підтримки актуальності здоров'я та прогресу захоплення.
    /// </summary>
    public class SelectionInfoPanelUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _subtitleText;
        [SerializeField] private TMP_Text _statsText;
        [SerializeField] private Transform _compactListRoot;
        [SerializeField] private TMP_Text _compactListTextPrefab;
        [SerializeField] private Transform _unitCardRoot;
        [SerializeField] private SelectionUnitCardUI _unitCardPrefab;
        [SerializeField, Min(1)] private int _maxVisibleUnitCards = 8;

        private readonly List<GameObject> _selectedUnits = new();
        private readonly List<TMP_Text> _compactRows = new();
        private readonly List<SelectionUnitCardUI> _unitCards = new();
        private readonly List<SelectionUnitGroup> _unitGroups = new();
        private readonly List<SelectionUnitCardViewModel> _visibleCardModels = new();
        private Object _selectedObject;
        private float _nextRefreshTime;

        private void OnEnable()
        {
            EventManager.OnUnitSelected += OnUnitSelected;
            EventManager.OnUnitDeselected += OnUnitDeselected;
            EventManager.OnFactorySelected += OnFactorySelected;
            EventManager.OnConstructionCenterSelected += OnConstructionCenterSelected;
            EventManager.OnOutpostSelected += OnOutpostSelected;
            EventManager.OnConstructionClosed += OnConstructionClosed;
            ShowIdle();
        }

        private void OnDisable()
        {
            EventManager.OnUnitSelected -= OnUnitSelected;
            EventManager.OnUnitDeselected -= OnUnitDeselected;
            EventManager.OnFactorySelected -= OnFactorySelected;
            EventManager.OnConstructionCenterSelected -= OnConstructionCenterSelected;
            EventManager.OnOutpostSelected -= OnOutpostSelected;
            EventManager.OnConstructionClosed -= OnConstructionClosed;
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

        /// <summary>Додає юніт до списку вибору та оновлює відображення.</summary>
        private void OnUnitSelected(GameObject unit)
        {
            if (unit == null)
                return;

            _selectedObject = null;

            if (!_selectedUnits.Contains(unit))
                _selectedUnits.Add(unit);

            RefreshUnits();
        }

        /// <summary>Видаляє юніт із вибору та або оновлює решту списку, або показує стан бездіяльності.</summary>
        private void OnUnitDeselected(GameObject unit)
        {
            if (unit != null)
                _selectedUnits.Remove(unit);

            if (_selectedUnits.Count > 0)
                RefreshUnits();
            else
                ShowIdle();
        }

        /// <summary>Перемикає відображення на інформацію про завод, очищуючи будь-який вибір юнітів.</summary>
        private void OnFactorySelected(BuildingProduction factory)
        {
            ClearUnits();
            _selectedObject = factory;
            RefreshObject();
        }

        /// <summary>Перемикає відображення на інформацію про будівельний центр, очищуючи будь-який вибір юнітів.</summary>
        private void OnConstructionCenterSelected(ConstructionCenter center)
        {
            ClearUnits();
            _selectedObject = center;
            RefreshObject();
        }

        /// <summary>Перемикає відображення на інформацію про аванпост, очищуючи будь-який вибір юнітів.</summary>
        private void OnOutpostSelected(Outpost outpost)
        {
            ClearUnits();
            _selectedObject = outpost;
            RefreshObject();
        }

        /// <summary>
        /// Закриття будівельної панелі не повинно скидати відображення юнітів:
        /// SelectionManager будівель також отримує mouse release після drag-select юнітів.
        /// </summary>
        private void OnConstructionClosed()
        {
            _selectedObject = null;

            if (_selectedUnits.Count > 0)
            {
                RefreshUnits();
                return;
            }

            ShowIdle();
        }

        /// <summary>Скидає панель до стандартного тексту-заповнювача "немає вибору".</summary>
        private void ShowIdle()
        {
            ClearUnits();
            _selectedObject = null;
            SetInfoTextVisible(true);
            SetText("No selection", "Select units or buildings", "Orders and object data will appear here.");
            SetCompactRows(null);
            SetUnitCards(null);
        }

        /// <summary>
        /// Видаляє знищені юніти з вибору, потім відображає характеристики одного юніта або
        /// компактний список кількох юнітів залежно від кількості у виборі.
        /// </summary>
        private void RefreshUnits()
        {
            RemoveDeadUnits();

            if (_selectedUnits.Count == 0)
            {
                ShowIdle();
                return;
            }

            SetInfoTextVisible(false);
            SetText(string.Empty, string.Empty, string.Empty);
            SetCompactRows(null);
            SetUnitCards(BuildUnitCardModels());
        }

        /// <summary>
        /// Відображає інформаційний текст для поточного вибраного не-юніт об'єкта (завод, будівельний центр
        /// або аванпост), зіставляючи шаблон за типом виконання <see cref="_selectedObject"/>.
        /// </summary>
        private void RefreshObject()
        {
            if (_selectedObject is BuildingProduction factory)
            {
                SetInfoTextVisible(true);
                TeamComponent team = factory.GetComponent<TeamComponent>();
                int itemCount = factory.Items.Count;
                SetText(
                    GetDisplayName(factory.name),
                    (team != null ? team.Team.ToString() : "Player") + " production",
                    $"Queue available\nUnits: {itemCount}\nUse the Units tab to train vehicles.");
                SetCompactRows(null);
                SetUnitCards(null);
                return;
            }

            if (_selectedObject is ConstructionCenter center)
            {
                SetInfoTextVisible(true);
                TeamComponent team = center.GetComponent<TeamComponent>();
                SetText(
                    GetDisplayName(center.name),
                    (team != null ? team.Team.ToString() : "Player") + " construction center",
                    $"Build radius {FormatNumber(center.BuildRadius)}\nUse the Build tab to place structures.");
                SetCompactRows(null);
                SetUnitCards(null);
                return;
            }

            if (_selectedObject is Outpost outpost)
            {
                SetInfoTextVisible(true);
                string owner = outpost.Owner.HasValue ? outpost.Owner.Value.ToString() : "Neutral";
                string state = outpost.IsUpgraded ? "Upgraded" : "Standard";
                SetText(
                    GetDisplayName(outpost.name),
                    owner + " capture point",
                    $"{state}\nIncome {FormatNumber(outpost.CurrentResourcePerMinute)}/min\n" +
                    $"Capture {FormatNumber(outpost.CaptureProgress * 100f)}%");
                SetCompactRows(null);
                SetUnitCards(null);
                return;
            }

            ShowIdle();
        }

        /// <summary>Очищає список вибраних юнітів та видаляє рядки компактного списку.</summary>
        private void ClearUnits()
        {
            _selectedUnits.Clear();
            SetCompactRows(null);
            SetUnitCards(null);
        }

        /// <summary>Видаляє нульові (знищені) записи зі списку вибраних юнітів.</summary>
        private void RemoveDeadUnits()
        {
            for (int i = _selectedUnits.Count - 1; i >= 0; i--)
            {
                if (_selectedUnits[i] == null)
                    _selectedUnits.RemoveAt(i);
            }
        }

        /// <summary>Призначає текст полям заголовка, підзаголовка та характеристик.</summary>
        private void SetText(string title, string subtitle, string stats)
        {
            if (_titleText != null)
                _titleText.text = title;

            if (_subtitleText != null)
                _subtitleText.text = subtitle;

            if (_statsText != null)
                _statsText.text = stats;
        }

        private void SetInfoTextVisible(bool visible)
        {
            if (_titleText != null)
                _titleText.gameObject.SetActive(visible);

            if (_subtitleText != null)
                _subtitleText.gameObject.SetActive(visible);

            if (_statsText != null)
                _statsText.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Знищує старі мітки компактних рядків та створює новий набір з <paramref name="rows"/>.
        /// Передача null очищає список без створення нових записів.
        /// </summary>
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

        private List<SelectionUnitCardViewModel> BuildUnitCardModels()
        {
            _unitGroups.Clear();

            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                GameObject unit = _selectedUnits[i];

                if (unit == null)
                    continue;

                UnitCombat combat = unit.GetComponent<UnitCombat>();
                UnitData data = combat != null ? combat.UnitData : null;
                string key = data != null ? data.GetInstanceID().ToString() : GetDisplayName(unit.name);
                int groupIndex = FindGroupIndex(key);

                if (groupIndex >= 0)
                {
                    _unitGroups[groupIndex].Count++;
                    continue;
                }

                string displayName = ResolveUnitDisplayName(data, unit);
                _unitGroups.Add(new SelectionUnitGroup(
                    key,
                    displayName,
                    data != null ? data.SelectionIcon : null,
                    ResolveFallbackText(data, displayName),
                    1));
            }

            _visibleCardModels.Clear();
            int maxCards = Mathf.Max(1, _maxVisibleUnitCards);
            int regularCards = _unitGroups.Count <= maxCards ? _unitGroups.Count : maxCards - 1;

            for (int i = 0; i < regularCards; i++)
            {
                SelectionUnitGroup group = _unitGroups[i];
                _visibleCardModels.Add(new SelectionUnitCardViewModel(
                    group.DisplayName,
                    group.Icon,
                    group.FallbackText,
                    group.Count));
            }

            if (_unitGroups.Count > maxCards)
            {
                int hiddenCount = 0;

                for (int i = regularCards; i < _unitGroups.Count; i++)
                    hiddenCount += _unitGroups[i].Count;

                _visibleCardModels.Add(new SelectionUnitCardViewModel("More", null, "+", hiddenCount));
            }

            return _visibleCardModels;
        }

        private int FindGroupIndex(string key)
        {
            for (int i = 0; i < _unitGroups.Count; i++)
            {
                if (_unitGroups[i].Key == key)
                    return i;
            }

            return -1;
        }

        private void SetUnitCards(List<SelectionUnitCardViewModel> models)
        {
            if (_unitCardRoot == null || _unitCardPrefab == null)
                return;

            int desiredCount = models != null ? models.Count : 0;
            _unitCardRoot.gameObject.SetActive(desiredCount > 0);

            while (_unitCards.Count < desiredCount)
            {
                SelectionUnitCardUI card = Instantiate(_unitCardPrefab, _unitCardRoot);
                _unitCards.Add(card);
            }

            for (int i = 0; i < _unitCards.Count; i++)
            {
                SelectionUnitCardUI card = _unitCards[i];

                if (card == null)
                    continue;

                bool active = i < desiredCount;
                card.gameObject.SetActive(active);

                if (active)
                    card.SetData(models[i]);
            }
        }

        private static string ResolveUnitDisplayName(UnitData data, GameObject unit)
        {
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.name))
                    return GetDisplayName(data.name);
            }

            return unit != null ? GetDisplayName(unit.name) : "Unknown";
        }

        private static string ResolveFallbackText(UnitData data, string displayName)
        {
            if (data != null && !string.IsNullOrWhiteSpace(data.SelectionFallbackText))
                return data.SelectionFallbackText;

            return BuildInitials(displayName);
        }

        private static string BuildInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "UNIT";

            string[] words = displayName.Split(' ');
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

        /// <summary>Видаляє суфікси Unity та внутрішні угоди про іменування з назви GameObject для відображення.</summary>
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

        /// <summary>Форматує число з плаваючою точкою як ціле, якщо воно ціле, інакше до двох знаків після коми.</summary>
        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }

        private sealed class SelectionUnitGroup
        {
            public SelectionUnitGroup(string key, string displayName, Sprite icon, string fallbackText, int count)
            {
                Key = key;
                DisplayName = displayName;
                Icon = icon;
                FallbackText = fallbackText;
                Count = count;
            }

            public string Key { get; }
            public string DisplayName { get; }
            public Sprite Icon { get; }
            public string FallbackText { get; }
            public int Count { get; set; }
        }
    }
}
