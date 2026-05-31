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

        private readonly List<ProductionButtonUI> _buttons = new();
        private BuildingProduction _currentFactory;

        private void OnEnable()
        {
            EventManager.OnFactorySelected += OpenFactory;
            ResourceManager.OnResourceChanged += RefreshButtons;
            BuildingProduction.FactoriesChanged += RefreshCurrentFactory;
            OpenFactory(GetInitialFactory());
        }

        private void OnDisable()
        {
            EventManager.OnFactorySelected -= OpenFactory;
            ResourceManager.OnResourceChanged -= RefreshButtons;
            BuildingProduction.FactoriesChanged -= RefreshCurrentFactory;
        }

        /// <summary>
        /// Повторно визначає, який завод відображати, після додавання або видалення заводу зі сцени.
        /// Повертається до <see cref="GetInitialFactory"/>, якщо поточний завод стає недійсним.
        /// </summary>
        private void RefreshCurrentFactory()
        {
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

            ClearButtons();

            int validItems = CountValidItems(factory);
            ApplyLayout(validItems);

            if (factory == null)
            {
                SetEmptyText(string.Empty);
                return;
            }

            SetEmptyText(string.Empty);

            foreach (ProductionItemData item in factory.Items)
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
        }

        /// <summary>
        /// Передає клік кнопки виробництва до черги заводу, потім оновлює доступність за ресурсами.
        /// </summary>
        private void OnItemClicked(ProductionItemData item)
        {
            if (_currentFactory == null)
                return;

            _currentFactory.AddToQueue(item);
            RefreshButtons(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
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

        /// <summary>Повертає кількість ненульових записів <see cref="ProductionItemData"/> на заводі.</summary>
        private static int CountValidItems(BuildingProduction factory)
        {
            if (factory == null)
                return 0;

            int count = 0;

            foreach (ProductionItemData item in factory.Items)
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
            return teamComponent == null || teamComponent.Team == _team;
        }
    }
}
