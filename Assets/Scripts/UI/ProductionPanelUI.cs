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
    /// Displays the production queue buttons for the currently selected (or first available)
    /// player <see cref="BuildingProduction"/> factory. Rebuilds button rows when the factory
    /// changes and greys out items the player cannot afford via <see cref="ResourceManager"/>.
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
        /// Re-evaluates which factory to display after a factory is added or removed from the scene.
        /// Falls back to <see cref="GetInitialFactory"/> if the current one becomes invalid.
        /// </summary>
        private void RefreshCurrentFactory()
        {
            OpenFactory(_currentFactory != null && _currentFactory.isActiveAndEnabled
                ? _currentFactory
                : GetInitialFactory());
        }

        /// <summary>
        /// Clears existing buttons and spawns a fresh set of <see cref="ProductionButtonUI"/> rows
        /// for every valid <see cref="ProductionItemData"/> on <paramref name="factory"/>.
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
        /// Forwards a production button click to the factory's queue, then refreshes affordability.
        /// </summary>
        private void OnItemClicked(ProductionItemData item)
        {
            if (_currentFactory == null)
                return;

            _currentFactory.AddToQueue(item);
            RefreshButtons(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
        }

        /// <summary>
        /// Updates the interactable/color state of every button based on current player resources.
        /// Called on <see cref="ResourceManager.OnResourceChanged"/>.
        /// </summary>
        private void RefreshButtons(int playerResource)
        {
            foreach (ProductionButtonUI button in _buttons)
            {
                if (button != null)
                    button.RefreshAvailability(playerResource);
            }
        }

        /// <summary>Destroys all instantiated button children and clears the button list.</summary>
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
        /// Configures the <see cref="GridLayoutGroup"/> and <see cref="ContentSizeFitter"/> on the
        /// content root to a fixed 3-column layout that auto-sizes its height.
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

        /// <summary>Forces an immediate layout recalculation on the content root RectTransform.</summary>
        private void RebuildLayout()
        {
            if (_contentRoot is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        /// <summary>Shows or hides the empty-state label with the given message.</summary>
        private void SetEmptyText(string message)
        {
            if (_emptyText == null)
                return;

            _emptyText.text = message;
            _emptyText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        /// <summary>Returns the number of non-null <see cref="ProductionItemData"/> entries on the factory.</summary>
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
        /// Returns the best factory to display: the currently selected one first,
        /// then the first active player-team factory found in <see cref="BuildingProduction.All"/>.
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

        /// <summary>Returns true if <paramref name="component"/> is owned by the panel's configured team.</summary>
        private bool BelongsToTeam(Component component)
        {
            TeamComponent teamComponent = component.GetComponentInParent<TeamComponent>();
            return teamComponent == null || teamComponent.Team == _team;
        }
    }
}
