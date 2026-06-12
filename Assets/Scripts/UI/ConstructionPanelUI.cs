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
    /// Відображає сітку кнопок розміщення будівель, що керується списком ресурсів <see cref="BuildingData"/>.
    /// Кнопки делегують до <see cref="BuildingPlacementManager.StartPlacement"/> і вимкнені, коли
    /// жоден <see cref="ConstructionCenter"/> гравця не є активним.
    /// </summary>
    public class ConstructionPanelUI : MonoBehaviour
    {
        private static readonly Vector2 ButtonSize = new Vector2(116f, 108f);
        private static readonly Color ButtonFillColor = new Color(0.03f, 0.45f, 0.85f, 1f);
        private static readonly Color DisabledFillColor = new Color(0.48f, 0.54f, 0.58f, 0.85f);
        private static readonly Color CostAffordableColor = Color.white;
        private static readonly Color CostUnaffordableColor = new Color(1f, 0.42f, 0.32f, 1f);

        [SerializeField] private Transform _contentRoot;
        [SerializeField] private TMP_Text _emptyText;
        [SerializeField] private BuildingPlacementManager _placementManager;
        [SerializeField] private List<BuildingData> _buildings = new();
        [SerializeField] private TeamType _team = TeamType.Player;
        [SerializeField] private TMP_FontAsset _fontAsset;
        [SerializeField] private Sprite _buttonSprite;

        private readonly List<Button> _buttons = new();
        private readonly List<BuildingData> _buttonBuildings = new();
        private readonly List<TMP_Text> _costTexts = new();

        private void OnEnable()
        {
            EventManager.OnConstructionCentersChanged += RefreshAvailability;
            ResourceManager.OnTeamResourceChanged += OnTeamResourceChanged;
            BuildButtons();
            RefreshAvailability();
        }

        private void OnDisable()
        {
            EventManager.OnConstructionCentersChanged -= RefreshAvailability;
            ResourceManager.OnTeamResourceChanged -= OnTeamResourceChanged;
        }

        /// <summary>
        /// Очищає кореневий контейнер та процедурно створює одну стилізовану кнопку для кожного
        /// запису <see cref="BuildingData"/>, потім викликає <see cref="RefreshAvailability"/>.
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

                Button button = CreateButton(building, out TMP_Text costText);
                _buttons.Add(button);
                _buttonBuildings.Add(building);
                _costTexts.Add(costText);
            }

            RebuildGrid();
            RefreshAvailability();
        }

        /// <summary>
        /// Процедурно будує повністю стилізований GameObject кнопки для однієї будівлі,
        /// включаючи іконку або резервний текст, мітку назви, вартість та піделементи часу будівництва.
        /// </summary>
        private Button CreateButton(BuildingData building, out TMP_Text costText)
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
            costText = CreateText(buttonObject.transform, "Cost", "$" + Mathf.Max(0, building.EconomyCost), 14f, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(9f, 8f), new Vector2(48f, 21f), false);
            CreateText(buttonObject.transform, "Time", FormatSeconds(building.BuildTime), 14f, FontStyles.Normal,
                TextAlignmentOptions.Right, new Vector2(-9f, 8f), new Vector2(50f, 20f), false);

            return button;
        }

        /// <summary>
        /// Створює дочірній Image для іконки будівлі; Image вимкнено, якщо <paramref name="icon"/> є null.
        /// Повертає true, якщо спрайт іконки є дійсним (виклик використовує це для пропуску резервного тексту).
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
        /// Створює стилізований дочірній TMP_Text, прив'язаний до верхнього центру, нижнього лівого або нижнього правого
        /// залежно від прапорця <paramref name="top"/> та знаку <paramref name="position"/>.x.
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
        /// Перевіряє наявність <see cref="ConstructionCenter"/> гравця, потім делегує до
        /// <see cref="BuildingPlacementManager.StartPlacement"/> для запуску режиму привидного розміщення.
        /// </summary>
        private void StartPlacement(BuildingData building)
        {
            if (_placementManager == null)
                _placementManager = FindFirstObjectByType<BuildingPlacementManager>();

            if (_placementManager == null || building == null)
                return;

            if (!HasPlayerConstructionCenter())
                return;

            if (!CanAfford(building))
                return;

            _placementManager.StartPlacement(building);
        }

        /// <summary>
        /// Вмикає або вимикає всі кнопки залежно від наявності будівельного центру гравця.
        /// Викликається на <see cref="EventManager.OnConstructionCentersChanged"/>.
        /// </summary>
        private void RefreshAvailability()
        {
            bool hasBuildArea = HasPlayerConstructionCenter();

            for (int i = 0; i < _buttons.Count; i++)
            {
                Button button = _buttons[i];
                if (button == null)
                    continue;

                BuildingData building = i < _buttonBuildings.Count ? _buttonBuildings[i] : null;
                bool hasResources = CanAfford(building);
                bool isAvailable = hasBuildArea && hasResources;

                button.interactable = isAvailable;

                if (button.targetGraphic is Image background)
                    background.color = isAvailable ? ButtonFillColor : DisabledFillColor;

                if (i < _costTexts.Count && _costTexts[i] != null)
                    _costTexts[i].color = hasResources ? CostAffordableColor : CostUnaffordableColor;
            }
        }

        private void OnTeamResourceChanged(TeamType team, int amount)
        {
            if (team == ResolveTeam())
                RefreshAvailability();
        }

        /// <summary>Знищує всі дочірні GameObject під кореневим контейнером та очищає список кнопок.</summary>
        private void ClearButtons()
        {
            if (_contentRoot != null)
            {
                foreach (Transform child in _contentRoot)
                    Destroy(child.gameObject);
            }

            _buttons.Clear();
            _buttonBuildings.Clear();
            _costTexts.Clear();
        }

        /// <summary>Налаштовує GridLayoutGroup та ContentSizeFitter для 3-колонкового макету з автоматичною висотою.</summary>
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

        /// <summary>Примусово виконує негайний перерахунок макету для RectTransform кореневого контейнера.</summary>
        private void RebuildGrid()
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

        /// <summary>Повертає зрозумілу для людини назву будівлі, очищуючи угоди про іменування ресурсів.</summary>
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

        /// <summary>Повертає true, якщо в сцені існує хоча б один активний <see cref="ConstructionCenter"/> команди гравця.</summary>
        private bool HasPlayerConstructionCenter()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (center != null && center.isActiveAndEnabled && BelongsToTeam(center))
                    return true;
            }

            return false;
        }

        /// <summary>Повертає true, якщо <paramref name="component"/> належить налаштованій команді панелі.</summary>
        private bool BelongsToTeam(Component component)
        {
            if (component is ConstructionCenter constructionCenter)
                return constructionCenter.BelongsToTeam(ResolveTeam());

            TeamComponent teamComponent = component.GetComponentInParent<TeamComponent>();
            return teamComponent != null && teamComponent.Team == ResolveTeam();
        }

        private TeamType ResolveTeam()
        {
            return _team == TeamType.Player
                ? LocalPlayerContext.LocalTeam
                : _team;
        }

        /// <summary>Перевіряє, чи команда панелі має достатньо ресурсів для конкретної будівлі.</summary>
        private bool CanAfford(BuildingData building)
        {
            if (building == null)
                return false;

            int cost = Mathf.Max(0, building.EconomyCost);
            if (cost <= 0 || ResourceManager.Instance == null)
                return true;

            return ResourceManager.Instance.GetResource(ResolveTeam()) >= cost;
        }
    }
}
