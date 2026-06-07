using System.Collections.Generic;
using Strategy.Data;
using Strategy.Core;
using Strategy.Buildings;
using TMPro;
using Strategy.Units;
using UnityEngine;
using UnityEngine.UI;

using Strategy.UI;
namespace Strategy.UI
{
    /// <summary>
    /// Відображає кнопки черги виробництва для поточного вибраного (або першого доступного)
    /// заводу гравця <see cref="BuildingProduction"/>. Перебудовує рядки кнопок при зміні заводу
    /// та робить неактивними позиції, які гравець не може собі дозволити, через <see cref="ResourceManager"/>.
    /// </summary>
    public class ProductionPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private ProductionButtonUI _buttonPrefab;
        [SerializeField] private TMP_Text _emptyText;
        [SerializeField] private TeamType _team = TeamType.Player;
        [SerializeField] private TMP_FontAsset _fontAsset;
        [SerializeField] private Sprite _buttonSprite;
        [SerializeField, Min(0.03f)] private float _productionStateRefreshInterval = 0.1f;

        private readonly List<ProductionButtonUI> _buttons = new();
        private readonly List<BuildingProduction> _selectedFactories = new();
        private readonly List<ProductionItemData> _displayItems = new();
        private readonly List<BuildingProduction> _stateFactories = new();
        private BuildingProduction _currentFactory;
        private float _nextProductionStateRefreshTime;

        private void OnEnable()
        {
            EventManager.OnFactorySelected += OpenFactory;
            EventManager.OnSelectionChanged += OnSelectionChanged;
            ResourceManager.OnResourceChanged += RefreshButtons;
            BuildingProduction.FactoriesChanged += RefreshCurrentFactory;
            OpenFactory(GetInitialFactory());
        }

        private void OnDisable()
        {
            EventManager.OnFactorySelected -= OpenFactory;
            EventManager.OnSelectionChanged -= OnSelectionChanged;
            ResourceManager.OnResourceChanged -= RefreshButtons;
            BuildingProduction.FactoriesChanged -= RefreshCurrentFactory;
        }

        private void Update()
        {
            if (_buttons.Count == 0 || Time.unscaledTime < _nextProductionStateRefreshTime)
                return;

            _nextProductionStateRefreshTime = Time.unscaledTime + _productionStateRefreshInterval;
            RefreshProductionStates();
        }

        /// <summary>
        /// Повторно визначає, який завод відображати, після додавання або видалення заводу зі сцени.
        /// Повертається до <see cref="GetInitialFactory"/>, якщо поточний завод стає недійсним.
        /// </summary>
        private void RefreshCurrentFactory()
        {
            PruneSelectedFactories();

            if (_selectedFactories.Count > 0)
            {
                RebuildForCurrentSelection();
                return;
            }

            OpenFactory(_currentFactory != null && _currentFactory.isActiveAndEnabled
                ? _currentFactory
                : GetInitialFactory());
        }

        /// <summary>
        /// Очищає існуючі кнопки та створює новий набір рядків <see cref="ProductionButtonUI"/>
        /// для кожного дійсного <see cref="ProductionItemData"/> на <paramref name="factory"/>.
        /// </summary>
        private void OpenFactory(BuildingProduction factory)
        {
            if (factory != null && !BelongsToTeam(factory))
                factory = null;

            _currentFactory = factory;

            if (factory != null && (_selectedFactories.Count == 0 || !_selectedFactories.Contains(factory)))
            {
                _selectedFactories.Clear();
                _selectedFactories.Add(factory);
            }

            ClearButtons();

            BuildDisplayItems();
            int validItems = CountValidItems(_displayItems);
            ApplyLayout(validItems);

            if (_currentFactory == null && _selectedFactories.Count == 0)
            {
                SetEmptyText(string.Empty);
                return;
            }

            SetEmptyText(string.Empty);

            foreach (ProductionItemData item in _displayItems)
            {
                if (item == null)
                    continue;

                ProductionButtonUI button =
                    Instantiate(_buttonPrefab, _contentRoot);

                button.SetStyle(_fontAsset, _buttonSprite);
                button.Initialize(item, OnItemClicked);
                _buttons.Add(button);
            }

            RebuildLayout();
            RefreshButtons(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
            RefreshProductionStates();
        }

        /// <summary>
        /// Передає клік кнопки виробництва до черги заводу, потім оновлює доступність за ресурсами.
        /// </summary>
        private void OnItemClicked(ProductionItemData item)
        {
            if (item == null)
                return;

            PruneSelectedFactories();

            if (_selectedFactories.Count > 0)
            {
                FactoryProductionDistributor.TryQueueLeastLoaded(_selectedFactories, item, out _);
            }
            else if (_currentFactory != null)
            {
                _currentFactory.AddToQueue(item);
            }

            RefreshButtons(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
            RefreshProductionStates();
        }

        /// <summary>
        /// Оновлює стан інтерактивності та колір кожної кнопки залежно від поточних ресурсів гравця.
        /// Викликається на <see cref="ResourceManager.OnResourceChanged"/>.
        /// </summary>
        private void RefreshButtons(int playerResource)
        {
            foreach (ProductionButtonUI button in _buttons)
            {
                if (button != null)
                    button.RefreshAvailability(playerResource);
            }

            RefreshProductionStates();
        }

        private void RefreshProductionStates()
        {
            BuildStateFactories();

            for (int i = 0; i < _buttons.Count; i++)
            {
                ProductionButtonUI button = _buttons[i];
                if (button == null)
                    continue;

                button.SetProductionState(
                    ProductionButtonStateAggregator.Build(_stateFactories, button.Item));
            }
        }

        private void BuildStateFactories()
        {
            _stateFactories.Clear();
            PruneSelectedFactories();

            if (_selectedFactories.Count > 0)
            {
                for (int i = 0; i < _selectedFactories.Count; i++)
                {
                    BuildingProduction factory = _selectedFactories[i];
                    if (factory != null && !_stateFactories.Contains(factory))
                        _stateFactories.Add(factory);
                }

                return;
            }

            if (_currentFactory != null && _currentFactory.isActiveAndEnabled && BelongsToTeam(_currentFactory))
                _stateFactories.Add(_currentFactory);
        }

        /// <summary>Знищує всі створені дочірні кнопки та очищає список кнопок.</summary>
        private void ClearButtons()
        {
            if (_contentRoot == null)
            {
                _buttons.Clear();
                return;
            }

            foreach (Transform child in _contentRoot)
            {
                Destroy(child.gameObject);
            }

            _buttons.Clear();
        }

        /// <summary>
        /// Налаштовує <see cref="GridLayoutGroup"/> та <see cref="ContentSizeFitter"/> на кореневому
        /// контейнері для фіксованого 3-колонкового макету з автоматичним підбором висоти.
        /// </summary>
        private void ApplyLayout(int itemCount)
        {
            GridLayoutGroup grid = _contentRoot != null
                ? _contentRoot.GetComponent<GridLayoutGroup>()
                : null;

            if (grid != null)
            {
                grid.cellSize = new Vector2(116f, 108f);
                grid.spacing = new Vector2(8f, 8f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                grid.childAlignment = TextAnchor.UpperLeft;
            }

            ContentSizeFitter fitter = _contentRoot != null
                ? _contentRoot.GetComponent<ContentSizeFitter>()
                : null;

            if (fitter != null)
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Примусово виконує негайний перерахунок макету для RectTransform кореневого контейнера.</summary>
        private void RebuildLayout()
        {
            if (_contentRoot is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        /// <summary>Показує або приховує мітку порожнього стану із заданим повідомленням.</summary>
        private void SetEmptyText(string message)
        {
            if (_emptyText == null)
                return;

            _emptyText.text = message;
            _emptyText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void OnSelectionChanged(IReadOnlyList<GameObject> selection)
        {
            _selectedFactories.Clear();

            if (selection != null)
            {
                for (int i = 0; i < selection.Count; i++)
                {
                    GameObject selectedObject = selection[i];
                    if (selectedObject == null)
                        continue;

                    BuildingProduction factory = selectedObject.GetComponent<BuildingProduction>();
                    if (factory != null && factory.isActiveAndEnabled && BelongsToTeam(factory) &&
                        !_selectedFactories.Contains(factory))
                    {
                        _selectedFactories.Add(factory);
                    }
                }
            }

            if (_selectedFactories.Count == 0)
                return;

            _currentFactory = _selectedFactories[0];
            RebuildForCurrentSelection();
        }

        private void RebuildForCurrentSelection()
        {
            _currentFactory = _selectedFactories.Count > 0 ? _selectedFactories[0] : _currentFactory;
            OpenFactory(_currentFactory);
        }

        private void PruneSelectedFactories()
        {
            for (int i = _selectedFactories.Count - 1; i >= 0; i--)
            {
                BuildingProduction factory = _selectedFactories[i];
                if (factory != null && factory.isActiveAndEnabled && BelongsToTeam(factory))
                    continue;

                _selectedFactories.RemoveAt(i);
            }
        }

        private void BuildDisplayItems()
        {
            _displayItems.Clear();
            PruneSelectedFactories();

            if (_selectedFactories.Count > 0)
            {
                for (int i = 0; i < _selectedFactories.Count; i++)
                    AddFactoryItems(_selectedFactories[i]);

                return;
            }

            AddFactoryItems(_currentFactory);
        }

        private void AddFactoryItems(BuildingProduction factory)
        {
            if (factory == null)
                return;

            foreach (ProductionItemData item in factory.Items)
            {
                if (item != null && !ContainsEquivalentItem(_displayItems, item))
                    _displayItems.Add(item);
            }
        }

        private static bool ContainsEquivalentItem(
            IReadOnlyList<ProductionItemData> items,
            ProductionItemData candidate)
        {
            if (items == null || !IsValidProductionItem(candidate))
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                ProductionItemData item = items[i];
                if (item == candidate || IsEquivalentProductionItem(item, candidate))
                    return true;
            }

            return false;
        }

        private static bool IsEquivalentProductionItem(
            ProductionItemData item,
            ProductionItemData candidate)
        {
            if (!IsValidProductionItem(item) || !IsValidProductionItem(candidate))
                return false;

            if (item.UnitData == candidate.UnitData)
                return true;

            return item.UnitData.Prefab == candidate.UnitData.Prefab;
        }

        private static bool IsValidProductionItem(ProductionItemData item)
        {
            return item != null && item.UnitData != null && item.UnitData.Prefab != null;
        }

        private static int CountValidItems(IReadOnlyList<ProductionItemData> items)
        {
            if (items == null)
                return 0;

            int count = 0;

            foreach (ProductionItemData item in items)
            {
                if (item != null)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Повертає найкращий завод для відображення: спочатку поточний вибраний,
        /// потім перший активний завод команди гравця з <see cref="BuildingProduction.All"/>.
        /// </summary>
        private BuildingProduction GetInitialFactory()
        {
            if (SelectionManager.SelectedFactory != null && BelongsToTeam(SelectionManager.SelectedFactory))
                return SelectionManager.SelectedFactory;

            foreach (BuildingProduction factory in BuildingProduction.All)
            {
                if (factory != null && factory.isActiveAndEnabled && BelongsToTeam(factory))
                    return factory;
            }

            return null;
        }

        /// <summary>Повертає true, якщо <paramref name="component"/> належить налаштованій команді панелі.</summary>
        private bool BelongsToTeam(Component component)
        {
            TeamComponent teamComponent = component.GetComponentInParent<TeamComponent>();
            return teamComponent == null || teamComponent.Team == ResolveTeam();
        }

        private TeamType ResolveTeam()
        {
            return _team == TeamType.Player
                ? LocalPlayerContext.LocalTeam
                : _team;
        }
    }
}
