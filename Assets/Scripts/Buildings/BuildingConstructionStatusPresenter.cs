using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.Buildings
{
    [DisallowMultipleComponent]
    public sealed class BuildingConstructionStatusPresenter : MonoBehaviour
    {
        [SerializeField] private BuildingConstructionState _construction;
        [SerializeField] private GameObject _statusBarRoot;
        [SerializeField] private RectTransform _trackRect;
        [SerializeField] private RectTransform _fillRect;
        [SerializeField] private Image _fillImage;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private string _statusBarObjectName = "BuildingConstructionStatusBar";
        [SerializeField] private string _trackObjectName = "Track";
        [SerializeField] private string _fillObjectName = "Fill";
        [SerializeField] private string _timeTextObjectName = "TimeText";

        private void Awake()
        {
            CacheReferences();
            SetFillProgress(0f);
            SetVisible(false);
        }

        private void OnEnable()
        {
            CacheReferences();
            SetVisible(false);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void Update()
        {
            CacheReferences();

            if (!HasReferences() || _construction == null || !_construction.IsUnderConstruction)
            {
                SetVisible(false);
                return;
            }

            SetFillProgress(_construction.Progress);
            if (_timeText != null)
                _timeText.text = FormatRemainingSeconds(_construction.RemainingSeconds);

            SetVisible(true);
        }

        private void CacheReferences()
        {
            if (_construction == null)
                _construction = GetComponent<BuildingConstructionState>();

            if (_statusBarRoot == null)
            {
                Transform statusBar = transform.Find(_statusBarObjectName);
                if (statusBar != null)
                    _statusBarRoot = statusBar.gameObject;
            }

            Transform searchRoot = _statusBarRoot != null ? _statusBarRoot.transform : transform;

            if (_trackRect == null)
                _trackRect = FindRectTransform(searchRoot, _trackObjectName);

            if (_fillImage == null)
                _fillImage = FindImage(searchRoot, _fillObjectName);

            if (_fillRect == null && _fillImage != null)
                _fillRect = _fillImage.rectTransform;

            if (_timeText == null)
                _timeText = FindText(searchRoot, _timeTextObjectName);
        }

        private bool HasReferences()
        {
            return _statusBarRoot != null &&
                   _trackRect != null &&
                   _fillRect != null &&
                   _fillImage != null;
        }

        private void SetFillProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (_fillImage != null)
                _fillImage.fillAmount = progress;

            if (_fillRect == null)
                return;

            _fillRect.anchorMin = Vector2.zero;
            _fillRect.anchorMax = new Vector2(progress, 1f);
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
        }

        private void SetVisible(bool visible)
        {
            if (_statusBarRoot != null && _statusBarRoot.activeSelf != visible)
                _statusBarRoot.SetActive(visible);
        }

        private static string FormatRemainingSeconds(float seconds)
        {
            return Mathf.CeilToInt(Mathf.Max(0f, seconds)) + "s";
        }

        private static RectTransform FindRectTransform(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
                return null;

            RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                RectTransform rectTransform = rectTransforms[i];
                if (rectTransform != null && rectTransform.name == objectName)
                    return rectTransform;
            }

            return null;
        }

        private static Image FindImage(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
                return null;

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.name == objectName)
                    return image;
            }

            return null;
        }

        private static TMP_Text FindText(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
                return null;

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.name == objectName)
                    return text;
            }

            return null;
        }
    }
}
