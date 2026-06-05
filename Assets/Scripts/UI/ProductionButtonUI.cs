using System;
using Strategy.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.UI
{
    /// <summary>
    /// Самодостатня кнопка черги виробництва, яка відображає іконку, назву, вартість та час будівництва юніта.
    /// Обробляє підсвічування за доступністю ресурсів, відображення підказки при наведенні курсору,
    /// та делегує кліки наданому ззовні зворотному виклику. Єдиний спільний оверлей підказки
    /// створюється ліниво при першому використанні.
    /// </summary>
    public class ProductionButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private const float TooltipPadding = 12f;
        private static readonly Vector2 ButtonSize = new Vector2(116f, 108f);
        private static readonly Color ButtonFillColor = new Color(0.03f, 0.45f, 0.85f, 1f);
        private static readonly Color DisabledFillColor = new Color(0.48f, 0.54f, 0.58f, 0.85f);

        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _fallbackText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private Button _button;
        [SerializeField] private GameObject _queueBadgeRoot;
        [SerializeField] private TMP_Text _queueCountText;
        [SerializeField] private GameObject _progressRoot;
        [SerializeField] private Image _progressFill;
        [SerializeField] private Vector2 _tooltipOffset = new Vector2(0f, 10f);
        [SerializeField] private TMP_FontAsset _fontAsset;
        [SerializeField] private Sprite _buttonSprite;

        private ProductionItemData _item;
        private Action<ProductionItemData> _onClick;
        private ProductionItemViewModel _model;
        private Image _background;
        private static RectTransform _tooltipRoot;
        private static TMP_Text _tooltipText;
        private static Canvas _tooltipCanvas;

        public ProductionItemData Item => _item;

        /// <summary>Зберігає спільні ресурси шрифту та спрайту, що використовуються <see cref="ApplyLayout"/> та створенням підказки.</summary>
        public void SetStyle(TMP_FontAsset fontAsset, Sprite buttonSprite)
        {
            _fontAsset = fontAsset;
            _buttonSprite = buttonSprite;
        }

        /// <summary>
        /// Прив'язує <see cref="ProductionItemData"/> до цієї кнопки, підключає зворотний виклик кліку
        /// та виконує повне налаштування макету та прив'язку даних через <see cref="Bind"/>.
        /// </summary>
        public void Initialize(ProductionItemData item, Action<ProductionItemData> onClick)
        {
            _item = item;
            _onClick = onClick;

            CacheReferences();
            ApplyLayout();

            if (_item == null)
            {
                gameObject.SetActive(false);
                return;
            }

            Bind(ProductionItemViewModel.From(item, int.MaxValue));
            SetProductionState(ProductionButtonRuntimeState.Empty);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(Click);
            }
        }

        /// <summary>
        /// Оновлює лише стан інтерактивності та колір фону, відображаючи, чи може гравець
        /// зараз дозволити собі цю позицію. Викликається щоразу при зміні ресурсів.
        /// </summary>
        public void RefreshAvailability(int playerResource)
        {
            if (_button == null || _item == null)
                return;

            BindAvailability(ProductionItemViewModel.From(_item, playerResource));
        }

        public void SetProductionState(ProductionButtonRuntimeState state)
        {
            CacheStatusReferences();

            int pendingCount = state.PendingCount;
            bool hasPendingWork = pendingCount > 0;
            bool showProgress = hasPendingWork && state.HasActiveProgress;

            if (_queueBadgeRoot != null)
                _queueBadgeRoot.SetActive(hasPendingWork);

            if (_queueCountText != null)
                _queueCountText.text = hasPendingWork ? FormatQueueCount(pendingCount) : string.Empty;

            if (_progressRoot != null)
                _progressRoot.SetActive(showProgress);

            SetProgressFill(showProgress ? state.Progress : 0f);

            if (_timeText != null)
                _timeText.text = showProgress ? FormatRemainingSeconds(state.RemainingSeconds) : _model.TimeText;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowTooltip(eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            MoveTooltip(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        private void OnDisable()
        {
            HideTooltip();
        }

        /// <summary>Викликає наданий ззовні зворотний виклик із виробничою позицією цієї кнопки.</summary>
        private void Click()
        {
            _onClick?.Invoke(_item);
        }

        /// <summary>
        /// Заповнює всі візуальні піделементи (іконка, назва, вартість, час) з моделі представлення,
        /// потім делегує забарвлення за доступністю ресурсів до <see cref="BindAvailability"/>.
        /// </summary>
        private void Bind(ProductionItemViewModel model)
        {
            _model = model;

            if (_icon != null)
            {
                _icon.sprite = model.Icon;
                _icon.enabled = model.Icon != null;
            }

            if (_fallbackText != null)
            {
                _fallbackText.text = model.FallbackText;
                _fallbackText.gameObject.SetActive(model.Icon == null);
            }

            if (_nameText != null)
                _nameText.text = model.DisplayName;

            if (_costText != null)
                _costText.text = model.CostText;

            if (_timeText != null)
                _timeText.text = model.TimeText;

            BindAvailability(model);
        }

        /// <summary>Встановлює інтерактивність кнопки та колір фону на основі <see cref="ProductionItemViewModel.IsAffordable"/>.</summary>
        private void BindAvailability(ProductionItemViewModel model)
        {
            _model = model;

            if (_button != null)
                _button.interactable = model.IsAffordable;

            if (_background != null)
                _background.color = model.IsAffordable ? ButtonFillColor : DisabledFillColor;
        }

        /// <summary>
        /// Знаходить дочірні посилання на компоненти UI за пошуком імені, створюючи відсутні
        /// піделементи (<see cref="Image"/>, <see cref="TMP_Text"/>) під час виконання, якщо вони не знайдені.
        /// </summary>
        private void CacheReferences()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            _background = GetComponent<Image>();

            if (_icon == null || _icon.transform == transform)
                _icon = FindChild<Image>("Icon") ?? CreateImage("Icon");

            if (_nameText == null)
                _nameText = FindChild<TMP_Text>("NameText") ??
                    FindChild<TMP_Text>("Text (TMP)") ??
                    CreateText("NameText");

            if (_costText == null)
                _costText = FindChild<TMP_Text>("CostText") ?? CreateText("CostText");

            if (_timeText == null)
                _timeText = FindChild<TMP_Text>("TimeText") ?? CreateText("TimeText");

            if (_fallbackText == null)
                _fallbackText = FindChild<TMP_Text>("FallbackIcon") ?? CreateText("FallbackIcon");

            CacheStatusReferences();
        }

        private void CacheStatusReferences()
        {
            if (_queueBadgeRoot == null)
                _queueBadgeRoot = FindDescendant<RectTransform>("QueueBadge")?.gameObject;

            if (_queueCountText == null)
                _queueCountText = FindDescendant<TMP_Text>("QueueCountText");

            if (_progressRoot == null)
                _progressRoot = FindDescendant<RectTransform>("ProgressRoot")?.gameObject;

            if (_progressFill == null)
                _progressFill = FindDescendant<Image>("ProgressFill");
        }

        /// <summary>
        /// Задає розмір кореня кнопки, встановлює кольори фону, додає Outline, налаштовує
        /// кольорові стани кнопки та розміщує кожен піделемент у його фіксованому місці прив'язки.
        /// </summary>
        private void ApplyLayout()
        {
            RectTransform rootRect = transform as RectTransform;

            if (rootRect != null)
                rootRect.sizeDelta = ButtonSize;

            if (_background != null)
            {
                _background.color = ButtonFillColor;
                _background.sprite = null;
                _background.type = Image.Type.Simple;
            }

            Outline outline = GetComponent<Outline>();
            if (outline == null)
                outline = gameObject.AddComponent<Outline>();

            outline.effectColor = new Color(1f, 1f, 1f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);

            if (_button != null && _background != null)
            {
                _button.targetGraphic = _background;
                ColorBlock colors = _button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.78f, 0.86f, 1f, 1f);
                colors.pressedColor = Color.white;
                colors.selectedColor = new Color(0.78f, 0.86f, 1f, 1f);
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.78f);
                _button.colors = colors;
            }

            if (_icon != null)
            {
                _icon.color = Color.white;
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;
            }

            SetTextStyle(_fallbackText, 20f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetTextStyle(_nameText, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetTextStyle(_costText, 14f, FontStyles.Normal, TextAlignmentOptions.Left);
            SetTextStyle(_timeText, 14f, FontStyles.Normal, TextAlignmentOptions.Right);

            SetTopRect(_icon != null ? _icon.rectTransform : null, new Vector2(0f, -26f), new Vector2(54f, 34f));
            SetTopRect(_fallbackText != null ? _fallbackText.rectTransform : null, new Vector2(0f, -26f), new Vector2(82f, 32f));
            SetTopRect(_nameText != null ? _nameText.rectTransform : null, new Vector2(0f, -60f), new Vector2(106f, 30f));
            SetBottomLeftRect(_costText != null ? _costText.rectTransform : null, new Vector2(9f, 8f), new Vector2(48f, 21f));
            SetBottomRightRect(_timeText != null ? _timeText.rectTransform : null, new Vector2(-9f, 8f), new Vector2(50f, 21f));

            SetTopRightRect(_queueBadgeRoot != null ? _queueBadgeRoot.transform as RectTransform : null, new Vector2(-5f, -5f), new Vector2(28f, 22f));
            SetStretchBottomRect(_progressRoot != null ? _progressRoot.transform as RectTransform : null, 7f, 7f, 3f, 5f);

            if (_progressFill != null)
            {
                _progressFill.type = Image.Type.Simple;
                _progressFill.fillAmount = 0f;
                SetLeftFillRect(_progressFill.rectTransform, 0f);
            }
        }

        /// <summary>Шукає прямого дочірнього об'єкта за іменем та повертає запитаний компонент або null.</summary>
        private T FindChild<T>(string objectName) where T : Component
        {
            Transform child = transform.Find(objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private T FindDescendant<T>(string objectName) where T : Component
        {
            T[] components = GetComponentsInChildren<T>(true);

            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.name == objectName)
                    return component;
            }

            return null;
        }

        /// <summary>Створює дочірній GameObject із компонентом <see cref="Image"/> без рейкасту та повертає його.</summary>
        private Image CreateImage(string objectName)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            imageObject.transform.SetParent(transform, false);
            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Створює дочірній GameObject із компонентом <see cref="TextMeshProUGUI"/> без рейкасту та повертає його.</summary>
        private TMP_Text CreateText(string objectName)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.transform.SetParent(transform, false);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Застосовує налаштування шрифту, діапазону розміру, стилю, вирівнювання та кольору до мітки TMP.</summary>
        private static void SetTextStyle(
            TMP_Text text,
            float maxFontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            if (text == null)
                return;

            ProductionButtonUI owner = text.GetComponentInParent<ProductionButtonUI>();
            if (owner != null && owner._fontAsset != null)
                text.font = owner._fontAsset;

            text.fontSize = maxFontSize;
            text.fontStyle = fontStyle;
            text.enableAutoSizing = true;
            text.fontSizeMin = maxFontSize <= 14f ? 12f : 14f;
            text.fontSizeMax = maxFontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Truncate;
        }

        /// <summary>Прив'язує RectTransform до верхнього центру батьківського об'єкта із заданим зміщенням та розміром.</summary>
        private static void SetTopRect(RectTransform rectTransform, Vector2 offset, Vector2 size)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = offset;
            rectTransform.sizeDelta = size;
        }

        /// <summary>Прив'язує RectTransform до нижнього лівого кута батьківського об'єкта із заданим зміщенням та розміром.</summary>
        private static void SetBottomLeftRect(RectTransform rectTransform, Vector2 offset, Vector2 size)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.anchoredPosition = offset;
            rectTransform.sizeDelta = size;
        }

        /// <summary>Прив'язує RectTransform до нижнього правого кута батьківського об'єкта із заданим зміщенням та розміром.</summary>
        private static void SetBottomRightRect(RectTransform rectTransform, Vector2 offset, Vector2 size)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = offset;
            rectTransform.sizeDelta = size;
        }

        private static void SetTopRightRect(RectTransform rectTransform, Vector2 offset, Vector2 size)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = offset;
            rectTransform.sizeDelta = size;
        }

        private static void SetStretchBottomRect(
            RectTransform rectTransform,
            float left,
            float right,
            float bottom,
            float height)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = new Vector2((left - right) * 0.5f, bottom);
            rectTransform.sizeDelta = new Vector2(-(left + right), height);
        }

        private static void Stretch(RectTransform rectTransform, Vector2 minOffset, Vector2 maxOffset)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = minOffset;
            rectTransform.offsetMax = maxOffset;
        }

        private void SetProgressFill(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (_progressFill == null)
                return;

            _progressFill.type = Image.Type.Simple;
            _progressFill.fillAmount = progress;
            SetLeftFillRect(_progressFill.rectTransform, progress);
        }

        private static void SetLeftFillRect(RectTransform rectTransform, float progress)
        {
            if (rectTransform == null)
                return;

            progress = Mathf.Clamp01(progress);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = new Vector2(progress, 1f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Заповнює та позиціонує спільний оверлей підказки із характеристиками цієї позиції,
        /// ліниво створюючи GameObject підказки через <see cref="EnsureTooltip"/> за потреби.
        /// </summary>
        private void ShowTooltip(Vector2 screenPosition)
        {
            if (_item == null)
                return;

            EnsureTooltip();

            if (_tooltipRoot == null || _tooltipText == null)
                return;

            _tooltipText.text = _model.TooltipText;
            _tooltipText.ForceMeshUpdate();

            _tooltipRoot.sizeDelta = new Vector2(
                300f,
                Mathf.Max(170f, _tooltipText.preferredHeight + TooltipPadding * 2f));

            _tooltipRoot.gameObject.SetActive(true);
            _tooltipRoot.SetAsLastSibling();
            MoveTooltip(screenPosition);
        }

        /// <summary>
        /// Переміщує підказку так, щоб вона з'являлась над кнопкою, обмежуючись межами канвасу
        /// та перевертаючись донизу, якщо вона виходить за верхній край.
        /// </summary>
        private void MoveTooltip(Vector2 screenPosition)
        {
            if (_tooltipRoot == null || !_tooltipRoot.gameObject.activeSelf)
                return;

            RectTransform buttonRect = transform as RectTransform;
            RectTransform canvasRect = _tooltipCanvas != null
                ? _tooltipCanvas.transform as RectTransform
                : null;

            if (buttonRect == null || canvasRect == null)
            {
                _tooltipRoot.position = screenPosition + _tooltipOffset;
                return;
            }

            Vector3[] buttonCorners = new Vector3[4];
            Vector3[] canvasCorners = new Vector3[4];
            buttonRect.GetWorldCorners(buttonCorners);
            canvasRect.GetWorldCorners(canvasCorners);

            float x = buttonCorners[0].x;
            float y = buttonCorners[1].y + _tooltipOffset.y + _tooltipRoot.rect.height;

            float minX = canvasCorners[0].x + TooltipPadding;
            float maxX = canvasCorners[2].x - _tooltipRoot.rect.width - TooltipPadding;
            float minY = canvasCorners[0].y + _tooltipRoot.rect.height + TooltipPadding;
            float maxY = canvasCorners[2].y - TooltipPadding;

            x = Mathf.Clamp(x, minX, maxX);

            if (y > maxY)
                y = buttonCorners[0].y - _tooltipOffset.y;

            y = Mathf.Clamp(y, minY, maxY);

            _tooltipRoot.position = new Vector3(x, y, 0f);
        }

        /// <summary>Приховує спільний оверлей підказки, якщо він існує.</summary>
        private static void HideTooltip()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Створює спільну панель підказки (фонове зображення + TMP_Text) як оверлей рівня канвасу
        /// при першій необхідності. Наступні виклики повертаються одразу, якщо вже створено.
        /// </summary>
        private void EnsureTooltip()
        {
            if (_tooltipRoot != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();

            if (canvas == null)
                return;

            _tooltipCanvas = canvas;

            GameObject tooltipObject = new GameObject(
                "ProductionTooltip",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            tooltipObject.transform.SetParent(canvas.transform, false);

            _tooltipRoot = (RectTransform)tooltipObject.transform;
            _tooltipRoot.pivot = new Vector2(0f, 1f);
            _tooltipRoot.SetAsLastSibling();

            Image background = tooltipObject.GetComponent<Image>();
            background.color = new Color(0.95f, 0.98f, 1f, 0.97f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject(
                "TooltipText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.transform.SetParent(tooltipObject.transform, false);

            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(TooltipPadding, TooltipPadding);
            textRect.offsetMax = new Vector2(-TooltipPadding, -TooltipPadding);

            _tooltipText = textObject.GetComponent<TMP_Text>();
            if (_fontAsset != null)
                _tooltipText.font = _fontAsset;

            _tooltipText.fontSize = 18f;
            _tooltipText.enableAutoSizing = true;
            _tooltipText.fontSizeMin = 14f;
            _tooltipText.fontSizeMax = 18f;
            _tooltipText.color = new Color(0.08f, 0.1f, 0.12f, 1f);
            _tooltipText.alignment = TextAlignmentOptions.TopLeft;
            _tooltipText.raycastTarget = false;

            tooltipObject.SetActive(false);
        }

        /// <summary>
        /// Будує багаторядковий рядок підказки для позиції, додаючи повні характеристики юніта
        /// з <see cref="UnitData"/> за їх наявності.
        /// </summary>
        private static string BuildTooltipText(ProductionItemData item)
        {
            UnitData unit = item.UnitData;

            if (unit == null)
            {
                return
                    $"{FormatDisplayName(item.ItemName)}\n" +
                    $"Cost: {FormatCost(item.Cost)}\n" +
                    $"Build time: {FormatSeconds(item.ProductionTime)}";
            }

            return
                $"{FormatDisplayName(item.ItemName)}\n" +
                $"Cost: {FormatCost(item.Cost)}\n" +
                $"Build time: {FormatSeconds(item.ProductionTime)}\n\n" +
                "Stats\n" +
                $"Health: {FormatNumber(unit.MaxHealth)}\n" +
                $"Damage: {FormatNumber(unit.Damage)}\n" +
                $"Range: {FormatNumber(unit.AttackRange)}\n" +
                $"Attack delay: {FormatSeconds(unit.AttackDelay)}\n" +
                $"Speed: {FormatNumber(unit.Speed)}\n" +
                $"Formation: {FormatNumber(unit.FormationSpacing)}";
        }

        /// <summary>Очищує внутрішні угоди про іменування ресурсів (наприклад, "LightTank" → "Light Tank") для відображення.</summary>
        private static string FormatDisplayName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return "Unknown";

            return itemName
                .Replace("Meadl", "Medium")
                .Replace("LightTank", "Light Tank")
                .Replace("MediumTank", "Medium Tank");
        }

        private static string FormatCost(int cost)
        {
            return cost <= 0 ? "0" : cost.ToString();
        }

        private static string FormatSeconds(float seconds)
        {
            return $"{FormatNumber(seconds)}s";
        }

        private static string FormatQueueCount(int count)
        {
            if (count <= 0)
                return string.Empty;

            return count > 99 ? "99+" : count.ToString();
        }

        private static string FormatRemainingSeconds(float seconds)
        {
            if (seconds <= 0f)
                return "0s";

            if (seconds < 1f)
                return $"{seconds:0.0}s";

            return $"{Mathf.CeilToInt(seconds)}s";
        }

        /// <summary>Форматує число з плаваючою точкою як ціле, якщо воно не має дробової частини, інакше до двох знаків після коми.</summary>
        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }
    }
}
