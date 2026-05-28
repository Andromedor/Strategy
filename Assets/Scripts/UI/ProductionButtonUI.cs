using System;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public class ProductionButtonUI: MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private const float TooltipPadding = 12f;

        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private Button _button;
        [SerializeField] private Vector2 _tooltipOffset = new Vector2(0f, 10f);

        private ProductionItemData _item;
        private Action<ProductionItemData> _onClick;
        private static RectTransform _tooltipRoot;
        private static TMP_Text _tooltipText;
        private static Canvas _tooltipCanvas;

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

            if (item.Icon != null && _icon != null)
                _icon.sprite = item.Icon;

            if (_nameText != null)
                _nameText.text = FormatDisplayName(item.ItemName);

            if (_costText != null)
                _costText.text = $"Ціна: {FormatCost(item.Cost)}";

            if (_timeText != null)
                _timeText.text = $"Час: {FormatSeconds(item.ProductionTime)}";

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(Click);
            }
        }

        public void RefreshAvailability(int playerResource)
        {
            if (_button == null || _item == null)
                return;

            _button.interactable = _item.Cost <= playerResource;
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

        private void Click()
        {
            _onClick?.Invoke(_item);
        }

        private void CacheReferences()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            if (_icon == null)
                _icon = GetComponent<Image>();

            if (_nameText == null)
                _nameText = GetComponentInChildren<TMP_Text>(true);

            if (_costText == null)
                _costText = CreateText("CostText");

            if (_timeText == null)
                _timeText = CreateText("TimeText");
        }

        private void ApplyLayout()
        {
            RectTransform rootRect = transform as RectTransform;

            if (rootRect != null)
                rootRect.sizeDelta = new Vector2(190f, 64f);

            if (_icon != null)
                _icon.color = new Color(0.92f, 0.94f, 0.96f, 1f);

            SetTextStyle(_nameText, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTextStyle(_costText, 15f, FontStyles.Normal, TextAlignmentOptions.Left);
            SetTextStyle(_timeText, 15f, FontStyles.Normal, TextAlignmentOptions.Right);

            SetRect(
                _nameText != null ? _nameText.rectTransform : null,
                new Vector2(10f, -7f),
                new Vector2(170f, 24f),
                true);

            SetRect(
                _costText != null ? _costText.rectTransform : null,
                new Vector2(10f, 8f),
                new Vector2(82f, 22f),
                false);

            SetRect(
                _timeText != null ? _timeText.rectTransform : null,
                new Vector2(-10f, 8f),
                new Vector2(82f, 22f),
                false);
        }

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

        private static void SetTextStyle(
            TMP_Text text,
            float maxFontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            if (text == null)
                return;

            text.fontSize = maxFontSize;
            text.fontStyle = fontStyle;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = maxFontSize;
            text.alignment = alignment;
            text.color = new Color(0.1f, 0.12f, 0.14f, 1f);
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 offset,
            Vector2 size,
            bool topRow)
        {
            if (rectTransform == null)
                return;

            if (topRow)
            {
                rectTransform.anchorMin = new Vector2(0f, 1f);
                rectTransform.anchorMax = new Vector2(0f, 1f);
                rectTransform.pivot = new Vector2(0f, 1f);
                rectTransform.anchoredPosition = offset;
            }
            else if (offset.x < 0f)
            {
                rectTransform.anchorMin = new Vector2(1f, 0f);
                rectTransform.anchorMax = new Vector2(1f, 0f);
                rectTransform.pivot = new Vector2(1f, 0f);
                rectTransform.anchoredPosition = offset;
            }
            else
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.zero;
                rectTransform.pivot = Vector2.zero;
                rectTransform.anchoredPosition = offset;
            }

            rectTransform.sizeDelta = size;
        }

        private void ShowTooltip(Vector2 screenPosition)
        {
            if (_item == null)
                return;

            EnsureTooltip();

            if (_tooltipRoot == null || _tooltipText == null)
                return;

            _tooltipText.text = BuildTooltipText(_item);
            _tooltipText.ForceMeshUpdate();

            _tooltipRoot.sizeDelta = new Vector2(
                300f,
                Mathf.Max(210f, _tooltipText.preferredHeight + TooltipPadding * 2f));

            _tooltipRoot.gameObject.SetActive(true);
            _tooltipRoot.SetAsLastSibling();
            MoveTooltip(screenPosition);
        }

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

            if (x > maxX)
                x = maxX;

            if (x < minX)
                x = minX;

            if (y > maxY)
                y = buttonCorners[0].y - _tooltipOffset.y;

            y = Mathf.Clamp(y, minY, maxY);

            _tooltipRoot.position = new Vector3(x, y, 0f);
        }

        private static void HideTooltip()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.gameObject.SetActive(false);
        }

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
            background.color = new Color(0.96f, 0.98f, 1f, 0.96f);
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
            _tooltipText.fontSize = 16f;
            _tooltipText.enableAutoSizing = true;
            _tooltipText.fontSizeMin = 12f;
            _tooltipText.fontSizeMax = 16f;
            _tooltipText.color = new Color(0.08f, 0.1f, 0.12f, 1f);
            _tooltipText.alignment = TextAlignmentOptions.TopLeft;
            _tooltipText.raycastTarget = false;

            tooltipObject.SetActive(false);
        }

        private static string BuildTooltipText(ProductionItemData item)
        {
            UnitData unit = item.UnitData;

            if (unit == null)
            {
                return
                    $"{FormatDisplayName(item.ItemName)}\n" +
                    $"Ціна: {FormatCost(item.Cost)}\n" +
                    $"Час виробництва: {FormatSeconds(item.ProductionTime)}";
            }

            return
                $"{FormatDisplayName(item.ItemName)}\n" +
                $"Ціна: {FormatCost(item.Cost)}\n" +
                $"Час виробництва: {FormatSeconds(item.ProductionTime)}\n\n" +
                "Характеристики\n" +
                $"Здоров'я: {FormatNumber(unit.MaxHealth)}\n" +
                $"Шкода: {FormatNumber(unit.Damage)}\n" +
                $"Дальність атаки: {FormatNumber(unit.AttackRange)}\n" +
                $"Затримка атаки: {FormatSeconds(unit.AttackDelay)}\n" +
                $"Швидкість: {FormatNumber(unit.Speed)}\n" +
                $"Дистанція формації: {FormatNumber(unit.FormationSpacing)}\n" +
                $"Поворот башти: {FormatNumber(unit.TurretRotationSpeed)} град/с\n" +
                $"Нахил гармати: {FormatNumber(unit.MinGunPitch)} - {FormatNumber(unit.MaxGunPitch)} град";
        }

        private static string FormatDisplayName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return "Невідомий юніт";

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
            return $"{FormatNumber(seconds)} с";
        }

        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }
    }
}
