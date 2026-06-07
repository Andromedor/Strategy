using UnityEngine;

namespace Strategy.UI
{
    [DisallowMultipleComponent]
    public class SelectionDragBoxUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _boxRect;
        [SerializeField] private Canvas _canvas;

        private RectTransform _canvasRect;

        private void Awake()
        {
            CacheReferences();
            Hide();
        }

        public void Show(Vector2 startScreenPoint, Vector2 currentScreenPoint)
        {
            CacheReferences();

            if (_boxRect == null || _canvasRect == null)
                return;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            UpdateBox(startScreenPoint, currentScreenPoint);
        }

        public void UpdateBox(Vector2 startScreenPoint, Vector2 currentScreenPoint)
        {
            CacheReferences();

            if (_boxRect == null || _canvasRect == null)
                return;

            UnityEngine.Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    startScreenPoint,
                    uiCamera,
                    out Vector2 startLocal) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    currentScreenPoint,
                    uiCamera,
                    out Vector2 currentLocal))
            {
                return;
            }

            Vector2 min = Vector2.Min(startLocal, currentLocal);
            Vector2 max = Vector2.Max(startLocal, currentLocal);
            Vector2 size = max - min;

            _boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            _boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            _boxRect.pivot = new Vector2(0.5f, 0.5f);
            _boxRect.anchoredPosition = min + size * 0.5f;
            _boxRect.sizeDelta = size;
        }

        public void Hide()
        {
            if (_boxRect != null)
                _boxRect.sizeDelta = Vector2.zero;

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void CacheReferences()
        {
            if (_boxRect == null)
                _boxRect = transform as RectTransform;

            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();

            if (_canvasRect == null && _canvas != null)
                _canvasRect = _canvas.transform as RectTransform;
        }
    }
}
