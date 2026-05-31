using System.Collections.Generic;
using Strategy.Core;
using Strategy.Buildings;
using TMPro;
using Strategy.Units;
using UnityEngine;
using UnityEngine.UI;

using Strategy.Data;
using Strategy.UI;
namespace Strategy.UI
{
    /// <summary>
    /// Displays a grid of building placement buttons driven by a list of <see cref="BuildingData"/> assets.
    /// Buttons delegate to <see cref="BuildingPlacementManager.StartPlacement"/> and are disabled when
    /// no player-owned <see cref="ConstructionCenter"/> is active.
    /// </summary>
    public class ConstructionPanelUI : MonoBehaviour
    {
        private static readonly Vector2 ButtonSize = new Vector2(116f, 108f);
        private static readonly Color ButtonFillColor = new Color(0.03f, 0.45f, 0.85f, 1f);
        private static readonly Color DisabledFillColor = new Color(0.48f, 0.54f, 0.58f, 0.85f);

        [SerializeField] private Transform _contentRoot;
        [SerializeField] private TMP_Text _emptyText;
        [SerializeField] private BuildingPlacementManager _placementManager;
        [SerializeField] private List<BuildingData> _buildings = new();
        [SerializeField] private TeamType _team = TeamType.Player;
        [SerializeField] private TMP_FontAsset _fontAsset;
        [SerializeField] private Sprite _buttonSprite;

        private readonly List<Button> _buttons = new();

        private void OnEnable()
        {
            EventManager.OnConstructionCentersChanged += RefreshAvailability;
            BuildButtons();
            RefreshAvailability();
        }

        private void OnDisable()
        {
            EventManager.OnConstructionCentersChanged -= RefreshAvailability;
        }

        /// <summary>
        /// Clears the content root and procedurally creates one styled Button per
        /// <see cref="BuildingData"/> entry, then calls <see cref="RefreshAvailability"/>.
        /// </summary>
        public void BuildButtons()
        {
            ClearButtons();

            if (_contentRoot == null)
                return;

            if (_buildings == null || _buildings.Count == 0)
            {
                SetEmptyText("No buildings configured");
                return;
            }

            SetEmptyText(string.Empty);
            ApplyGrid();

            for (int i = 0; i < _buildings.Count; i++)
            {
                BuildingData building = _buildings[i];

                if (building == null)
                    continue;

                Button button = CreateButton(building);
                _buttons.Add(button);
            }

            RebuildGrid();
            RefreshAvailability();
        }

        /// <summary>
        /// Procedurally constructs a fully styled button GameObject for a single building,
        /// including icon or fallback text, name label, cost, and build-time sub-elements.
        /// </summary>
        private Button CreateButton(BuildingData building)
        {
            GameObject buttonObject = new GameObject(
                "Build_" + GetDisplayName(building),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

            buttonObject.transform.SetParent(_contentRoot, false);

            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.sizeDelta = ButtonSize;

            Image background = buttonObject.GetComponent<Image>();
            background.color = ButtonFillColor;
            background.sprite = null;
            background.type = Image.Type.Simple;

            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.pressedColor = Color.white;
            colors.selectedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.78f);
            button.colors = colors;
            button.onClick.AddListener(() => StartPlacement(building));

            CreateText(buttonObject.transform, "Action", "Build", 13f, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0f, -6f), new Vector2(96f, 18f), true);

            if (!CreateIcon(buttonObject.transform, building.Icon))
            {
                CreateText(buttonObject.transform, "FallbackIcon", "BUILD", 20f, FontStyles.Bold,
                    TextAlignmentOptions.Center, new Vector2(0f, -26f), new Vector2(82f, 32f), true);
            }

            CreateText(buttonObject.transform, "Name", GetDisplayName(building), 16f, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0f, -60f), new Vector2(106f, 30f), true);
            CreateText(buttonObject.transform, "Cost", "$" + Mathf.Max(0, building.EconomyCost), 14f, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(9f, 8f), new Vector2(48f, 21f), false);
            CreateText(buttonObject.transform, "Time", FormatSeconds(building.BuildTime), 14f, FontStyles.Normal,
                TextAlignmentOptions.Right, new Vector2(-9f, 8f), new Vector2(50f, 20f), false);

            return button;
        }

        /// <summary>
        /// Creates a child Image for the building icon; the Image is disabled when <paramref name="icon"/> is null.
        /// Returns true if the icon sprite is valid (caller uses this to skip the fallback text).
        /// </summary>
        private bool CreateIcon(Transform parent, Sprite icon)
        {
            GameObject iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            iconObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)iconObject.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -26f);
            rect.sizeDelta = new Vector2(54f, 34f);

            Image image = iconObject.GetComponent<Image>();
            image.sprite = icon;
            image.enabled = icon != null;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;

            return icon != null;
        }

        /// <summary>
        /// Creates a styled TMP_Text child anchored to the top-center, bottom-left, or bottom-right
        /// depending on the <paramref name="top"/> flag and the sign of <paramref name="position"/>.x.
        /// </summary>
        private TMP_Text CreateText(
            Transform parent,
            string objectName,
            string text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Vector2 position,
            Vector2 size,
            bool top)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)textObject.transform;
            if (top)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
            }
            else if (position.x < 0f)
            {
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
            }

            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TMP_Text label = textObject.GetComponent<TMP_Text>();
            if (_fontAsset != null)
                label.font = _fontAsset;

            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// Validates that a player <see cref="ConstructionCenter"/> exists, then delegates to
        /// <see cref="BuildingPlacementManager.StartPlacement"/> to begin ghost-placement mode.
        /// </summary>
        private void StartPlacement(BuildingData building)
        {
            if (_placementManager == null)
                _placementManager = FindFirstObjectByType<BuildingPlacementManager>();

            if (_placementManager == null || building == null)
                return;

            if (!HasPlayerConstructionCenter())
                return;

            _placementManager.StartPlacement(building);
        }

        /// <summary>
        /// Enables or disables all buttons depending on whether a player construction center is present.
        /// Called on <see cref="EventManager.OnConstructionCentersChanged"/>.
        /// </summary>
        private void RefreshAvailability()
        {
            bool hasBuildArea = HasPlayerConstructionCenter();

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                {
                    _buttons[i].interactable = hasBuildArea;
                    if (_buttons[i].targetGraphic is Image background)
                        background.color = hasBuildArea ? ButtonFillColor : DisabledFillColor;
                }
            }
        }

        /// <summary>Destroys all child GameObjects under the content root and clears the button list.</summary>
        private void ClearButtons()
        {
            if (_contentRoot != null)
            {
                foreach (Transform child in _contentRoot)
                    Destroy(child.gameObject);
            }

            _buttons.Clear();
        }

        /// <summary>Configures the GridLayoutGroup and ContentSizeFitter for a 3-column auto-height layout.</summary>
        private void ApplyGrid()
        {
            GridLayoutGroup grid = _contentRoot.GetComponent<GridLayoutGroup>();

            if (grid == null)
                return;

            grid.cellSize = ButtonSize;
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = _contentRoot.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Forces an immediate layout recalculation on the content root RectTransform.</summary>
        private void RebuildGrid()
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

        /// <summary>Returns a human-readable name for a building, cleaning up asset naming conventions.</summary>
        private static string GetDisplayName(BuildingData building)
        {
            string value = building != null && !string.IsNullOrWhiteSpace(building.BuildingName)
                ? building.BuildingName
                : building != null
                    ? building.name
                    : null;

            if (string.IsNullOrWhiteSpace(value))
                return "Building";

            return value
                .Replace("HeavyFactory", "Heavy Factory")
                .Replace("_", " ")
                .Trim();
        }

        private static string FormatSeconds(float seconds)
        {
            if (seconds <= 0f)
                return "0s";

            return Mathf.Approximately(seconds, Mathf.Round(seconds))
                ? Mathf.RoundToInt(seconds) + "s"
                : seconds.ToString("0.#") + "s";
        }

        /// <summary>Returns true if at least one active, player-team <see cref="ConstructionCenter"/> exists in the scene.</summary>
        private bool HasPlayerConstructionCenter()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (center != null && center.isActiveAndEnabled && BelongsToTeam(center))
                    return true;
            }

            return false;
        }

        /// <summary>Returns true if <paramref name="component"/> is owned by the panel's configured team.</summary>
        private bool BelongsToTeam(Component component)
        {
            TeamComponent teamComponent = component.GetComponentInParent<TeamComponent>();
            return teamComponent == null || teamComponent.Team == _team;
        }
    }
}
