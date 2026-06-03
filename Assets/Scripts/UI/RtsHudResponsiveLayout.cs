using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.UI
{
    /// <summary>
    /// Перепозиціонує та змінює розмір нижньої HUD-панелі та її підобластей (мінімапа, інформація
    /// про вибір, командна панель, верхня панель ресурсів) при кожній зміні розміру канвасу.
    /// Автоматично перемикається між широким альбомним та вузьким/портретним макетом.
    /// </summary>
    public class RtsHudResponsiveLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvasRoot;
        [SerializeField] private RectTransform _bottomHud;
        [SerializeField] private RectTransform _minimapSlot;
        [SerializeField] private RectTransform _selectionInfoPanel;
        [SerializeField] private RectTransform _controlGroupBar;
        [SerializeField] private RectTransform _commandDeck;

        private Vector2 _lastSize;

        private void Awake()
        {
            ApplyLayout();
        }

        private void Update()
        {
            if (_canvasRoot == null)
                return;

            Vector2 size = _canvasRoot.rect.size;

            if ((size - _lastSize).sqrMagnitude > 1f)
                ApplyLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout();
        }

        /// <summary>
        /// Вимірює розмір канвасу та делегує до <see cref="ApplyWide"/> або <see cref="ApplyNarrow"/>
        /// залежно від того, чи є вікно перегляду широким альбомним або вузьким/портретним.
        /// </summary>
        public void ApplyLayout()
        {
            if (_canvasRoot == null)
                _canvasRoot = transform as RectTransform;

            if (_canvasRoot == null)
                return;

            Vector2 size = _canvasRoot.rect.size;
            if (size.x <= 1f || size.y <= 1f)
                size = new Vector2(1920f, 1080f);

            _lastSize = size;

            bool narrow = size.x < 760f || size.y > size.x * 1.15f;

            if (narrow)
                ApplyNarrow(size);
            else
                ApplyWide(size);
        }

        /// <summary>
        /// Розміщує HUD для широкого/альбомного вікна перегляду: мінімапа ліворуч, командна панель праворуч,
        /// інформація про вибір по центру між ними, ресурси у верхньому правому куті.
        /// </summary>
        private void ApplyWide(Vector2 size)
        {
            float baseBottomHeight = Mathf.Clamp(size.y * 0.22f, 210f, 252f);
            float minimapWidth = Mathf.Clamp(size.x * 0.145f, 240f, 300f);
            float commandWidth = Mathf.Clamp(size.x * 0.235f, 390f, 450f);
            float padding = 12f;
            float frameExtra = 40f;
            float framePadding = padding + frameExtra;
            float controlGroupHeight = 48f;
            float controlGroupGap = 8f;
            float sideContentHeight = baseBottomHeight - padding * 2f;
            float selectionContentHeight = Mathf.Max(96f, sideContentHeight - controlGroupHeight - controlGroupGap);
            float bottomHeight = baseBottomHeight + frameExtra * 2f;

            SetStretchBottom(_bottomHud, bottomHeight);

            SetLeftPanel(_minimapSlot, minimapWidth, sideContentHeight, framePadding);
            SetRightPanel(_commandDeck, commandWidth, sideContentHeight, framePadding);
            SetCenterPanel(
                _selectionInfoPanel,
                minimapWidth + framePadding + padding,
                commandWidth + framePadding + padding,
                selectionContentHeight,
                framePadding);
            SetCenterBandAboveContent(
                _controlGroupBar,
                minimapWidth + framePadding + padding,
                commandWidth + framePadding + padding,
                selectionContentHeight,
                framePadding,
                controlGroupHeight,
                controlGroupGap);
        }

        /// <summary>
        /// Розміщує HUD для вузького/портретного вікна перегляду: мінімапа у верхньому лівому куті,
        /// інформація про вибір у верхньому правому куті, командна панель заповнює низ, ресурси у верхньому правому куті.
        /// </summary>
        private void ApplyNarrow(Vector2 size)
        {
            float baseBottomHeight = Mathf.Clamp(size.y * 0.5f, 370f, 450f);
            float minimapWidth = Mathf.Clamp(size.x * 0.34f, 128f, 164f);
            float minimapHeight = 108f;
            float commandHeight = Mathf.Clamp(baseBottomHeight * 0.5f, 172f, 220f);
            float padding = 10f;
            float frameExtra = 34f;
            float framePadding = padding + frameExtra;
            float controlGroupHeight = 48f;
            float controlGroupGap = 8f;
            float minInfoHeight = 118f;
            float infoHeight = baseBottomHeight - commandHeight - padding * 2f - controlGroupHeight - controlGroupGap;
            if (infoHeight < minInfoHeight)
            {
                commandHeight = Mathf.Max(150f, commandHeight - (minInfoHeight - infoHeight));
                infoHeight = minInfoHeight;
            }
            float bottomHeight = baseBottomHeight + frameExtra * 2f;

            SetStretchBottom(_bottomHud, bottomHeight);

            SetTopLeftInside(_minimapSlot, new Vector2(minimapWidth, minimapHeight), framePadding);
            SetTopStretchInside(
                _controlGroupBar,
                minimapWidth + framePadding + padding,
                framePadding,
                controlGroupHeight);
            SetTopStretchInside(
                _selectionInfoPanel,
                minimapWidth + framePadding + padding,
                framePadding + controlGroupHeight + controlGroupGap,
                infoHeight);
            SetBottomStretchInside(_commandDeck, commandHeight, framePadding);
        }

        /// <summary>Прив'язує RectTransform для розтягування на повну ширину знизу батьківського об'єкта із заданою висотою.</summary>
        private static void SetStretchBottom(RectTransform rect, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        /// <summary>Прив'язує RectTransform до нижнього лівого кута батьківського об'єкта із заданим розміром та відступом.</summary>
        private static void SetLeftPanel(RectTransform rect, float width, float height, float padding)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(padding, padding);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Прив'язує RectTransform до нижнього правого кута батьківського об'єкта із заданим розміром та відступом.</summary>
        private static void SetRightPanel(RectTransform rect, float width, float height, float padding)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-padding, padding);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// Розтягує RectTransform горизонтально, залишаючи <paramref name="left"/> та <paramref name="right"/>
        /// пікселів для бокових панелей, та позиціонує його із заданим відступом від низу.
        /// </summary>
        private static void SetCenterPanel(
            RectTransform rect,
            float left,
            float right,
            float height,
            float padding)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, padding);
            rect.sizeDelta = new Vector2(-(left + right), height);
        }

        /// <summary>Розміщує вузьку смугу над центральною нижньою панеллю, використовуючи ті самі горизонтальні межі.</summary>
        private static void SetCenterBandAboveContent(
            RectTransform rect,
            float left,
            float right,
            float contentHeight,
            float padding,
            float height,
            float gap)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, padding + contentHeight + gap);
            rect.sizeDelta = new Vector2(-(left + right), height);
        }

        /// <summary>Прив'язує RectTransform до верхнього лівого кута всередині батьківського об'єкта із заданим внутрішнім відступом.</summary>
        private static void SetTopLeftInside(RectTransform rect, Vector2 size, float padding)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(padding, -padding);
            rect.sizeDelta = size;
        }

        /// <summary>
        /// Розтягує RectTransform до верху батьківського об'єкта зі зміщенням <paramref name="left"/> пікселів
        /// ліворуч та симетричним обрізанням праворуч.
        /// </summary>
        private static void SetTopStretchInside(RectTransform rect, float left, float padding, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(left * 0.5f, -padding);
            rect.sizeDelta = new Vector2(-(left + padding), Mathf.Max(1f, height));
        }

        /// <summary>Розтягує RectTransform на повну ширину знизу батьківського об'єкта з рівномірними бічними відступами.</summary>
        private static void SetBottomStretchInside(RectTransform rect, float height, float padding)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, padding);
            rect.sizeDelta = new Vector2(-padding * 2f, height);
        }
    }
}
