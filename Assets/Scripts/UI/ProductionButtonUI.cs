using System;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
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
        [SerializeField] private Vector2 _tooltipOffset = new Vector2(0f, 10f);
        [SerializeField] private TMP_FontAsset _fontAsset;
        [SerializeField] private Sprite _buttonSprite;

        private ProductionItemData _item;
        private Action<ProductionItemData> _onClick;
        private Image _background;
        private static RectTransform _tooltipRoot;
        private static TMP_Text _tooltipText;
        private static Canvas _tooltipCanvas;

        public void SetStyle(TMP_FontAsset fontAsset, Sprite buttonSprite)
        {
            _fontAsset = fontAsset;
            _buttonSprite = buttonSprite;
        }

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

            if (_icon != null)
            {
                _icon.sprite = item.Icon;
                _icon.enabled = item.Icon != null;
            }

            if (_fallbackText != null)
            {
                _fallbackText.text = "UNIT";
                _fallbackText.gameObject.SetActive(item.Icon == null);
            }

            if (_nameText != null)
                _nameText.text = FormatDisplayName(item.ItemName);

            if (_costText != null)
                _costText.text = "$" + FormatCost(item.Cost);

            if (_timeText != null)
                _timeText.text = FormatSeconds(item.ProductionTime);

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

            if (_background != null)
                _background.color = _button.interactable
                    ? ButtonFillColor
                    : DisabledFillColor;
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
        }

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
        }

        private T FindChild<T>(string objectName) where T : Component
        {
            Transform child = transform.Find(objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

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
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

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
                Mathf.Max(170f, _tooltipText.preferredHeight + TooltipPadding * 2f));

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

            x = Mathf.Clamp(x, minX, maxX);

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

        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }
    }
}
