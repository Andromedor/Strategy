using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.UI
{
    /// <summary>
    /// Repositions and resizes the bottom HUD panel and its sub-regions (minimap, selection info,
    /// command deck, top resources bar) whenever the canvas size changes. Switches between a wide
    /// landscape layout and a narrow/portrait layout automatically.
    /// </summary>
    public class RtsHudResponsiveLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvasRoot;
        [SerializeField] private RectTransform _bottomHud;
        [SerializeField] private RectTransform _minimapSlot;
        [SerializeField] private RectTransform _selectionInfoPanel;
        [SerializeField] private RectTransform _commandDeck;
        [SerializeField] private RectTransform _topResources;

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
        /// Measures the canvas size and delegates to <see cref="ApplyWide"/> or <see cref="ApplyNarrow"/>
        /// based on whether the viewport is landscape-wide or narrow/portrait.
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
        /// Lays out the HUD for a wide/landscape viewport: minimap left, command deck right,
        /// selection info centered between them, resources at the top right.
        /// </summary>
        private void ApplyWide(Vector2 size)
        {
            float baseBottomHeight = Mathf.Clamp(size.y * 0.22f, 210f, 252f);
            float minimapWidth = Mathf.Clamp(size.x * 0.145f, 240f, 300f);
            float commandWidth = Mathf.Clamp(size.x * 0.235f, 390f, 450f);
            float padding = 12f;
            float frameExtra = 40f;
            float framePadding = padding + frameExtra;
            float contentHeight = baseBottomHeight - padding * 2f;
            float bottomHeight = baseBottomHeight + frameExtra * 2f;
            float resourceWidth = Mathf.Min(1100f, size.x - 28f);

            SetStretchBottom(_bottomHud, bottomHeight);
            SetTopRight(_topResources, new Vector2(resourceWidth, 86f), new Vector2(-14f, -14f));

            SetLeftPanel(_minimapSlot, minimapWidth, contentHeight, framePadding);
            SetRightPanel(_commandDeck, commandWidth, contentHeight, framePadding);
            SetCenterPanel(
                _selectionInfoPanel,
                minimapWidth + framePadding + padding,
                commandWidth + framePadding + padding,
                contentHeight,
                framePadding);
        }

        /// <summary>
        /// Lays out the HUD for a narrow/portrait viewport: minimap top-left, selection info
        /// top-right, command deck filling the bottom, resources at the top right.
        /// </summary>
        private void ApplyNarrow(Vector2 size)
        {
            float baseBottomHeight = Mathf.Clamp(size.y * 0.46f, 330f, 410f);
            float minimapWidth = Mathf.Clamp(size.x * 0.34f, 128f, 164f);
            float minimapHeight = 108f;
            float commandHeight = Mathf.Clamp(baseBottomHeight * 0.56f, 188f, 230f);
            float padding = 10f;
            float frameExtra = 34f;
            float framePadding = padding + frameExtra;
            float infoHeight = baseBottomHeight - commandHeight - padding * 2f;
            float bottomHeight = baseBottomHeight + frameExtra * 2f;
            float resourceWidth = Mathf.Min(900f, size.x - 20f);

            SetStretchBottom(_bottomHud, bottomHeight);
            SetTopRight(_topResources, new Vector2(resourceWidth, 82f), new Vector2(-10f, -10f));

            SetTopLeftInside(_minimapSlot, new Vector2(minimapWidth, minimapHeight), framePadding);
            SetTopStretchInside(
                _selectionInfoPanel,
                minimapWidth + framePadding + padding,
                framePadding,
                infoHeight);
            SetBottomStretchInside(_commandDeck, commandHeight, framePadding);
        }

        /// <summary>Anchors a RectTransform to stretch the full width at the bottom of its parent with the given height.</summary>
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

        /// <summary>Anchors a RectTransform to the top-right corner of its parent at the given offset and size.</summary>
        private static void SetTopRight(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Anchors a RectTransform to the bottom-left of its parent with the specified size and padding offset.</summary>
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

        /// <summary>Anchors a RectTransform to the bottom-right of its parent with the specified size and padding offset.</summary>
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
        /// Stretches a RectTransform horizontally, leaving <paramref name="left"/> and <paramref name="right"/>
        /// pixels reserved for the flanking panels, and positions it at the given padding from the bottom.
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

        /// <summary>Anchors a RectTransform to the top-left inside its parent at the given padding inset.</summary>
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
        /// Stretches a RectTransform to the top of its parent, offset by <paramref name="left"/> pixels on the
        /// left and trimmed symmetrically on the right, with a minimum height of 86 px.
        /// </summary>
        private static void SetTopStretchInside(RectTransform rect, float left, float padding, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(left * 0.5f, -padding);
            rect.sizeDelta = new Vector2(-(left + padding), Mathf.Max(86f, height));
        }

        /// <summary>Stretches a RectTransform to the full width at the bottom of its parent with uniform side padding.</summary>
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
