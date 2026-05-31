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

        private void RefreshCurrentFactory()
        {
            OpenFactory(_currentFactory != null && _currentFactory.isActiveAndEnabled
                ? _currentFactory
                : GetInitialFactory());
        }

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

        private void OnItemClicked(ProductionItemData item)
        {
            if (_currentFactory == null)
                return;

            _currentFactory.AddToQueue(item);
            RefreshButtons(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
        }

        private void RefreshButtons(int playerResource)
        {
            foreach (ProductionButtonUI button in _buttons)
            {
                if (button != null)
                    button.RefreshAvailability(playerResource);
            }
        }

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

        private void RebuildLayout()
        {
            if (_contentRoot is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private void SetEmptyText(string message)
        {
            if (_emptyText == null)
                return;

            _emptyText.text = message;
            _emptyText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

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

        private bool BelongsToTeam(Component component)
        {
            TeamComponent teamComponent = component.GetComponentInParent<TeamComponent>();
            return teamComponent == null || teamComponent.Team == _team;
        }
    }
}
