using UnityEngine;
using UnityEngine.UI;

namespace Strategy.Buildings
{
    public class FactoryProductionStatusPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _statusBarRoot;
        [SerializeField] private RectTransform _trackRect;
        [SerializeField] private RectTransform _fillRect;
        [SerializeField] private Image _fillImage;
        [SerializeField] private string _trackObjectName = "Track";
        [SerializeField] private string _fillObjectName = "Fill";

        private BuildingProduction _production;
        private BuildingSelectionState _selectionState;

        private void Awake()
        {
            _production = GetComponent<BuildingProduction>();
            _selectionState = GetComponent<BuildingSelectionState>();
            CacheStatusBarReferences();
            SetFillProgress(0f);
        }

        private void OnEnable()
        {
            CacheStatusBarReferences();
            SetVisible(false);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void Update()
        {
            if (_production == null)
                _production = GetComponent<BuildingProduction>();

            if (_selectionState == null)
                _selectionState = GetComponent<BuildingSelectionState>();

            CacheStatusBarReferences();

            if (!HasStatusBarReferences() ||
                _production == null ||
                _selectionState == null ||
                !_selectionState.IsSelected ||
                !_production.TryGetCurrentProduction(out FactoryProductionRuntimeState state))
            {
                SetVisible(false);
                return;
            }

            SetFillProgress(state.Progress);
            SetVisible(true);
        }

        private void CacheStatusBarReferences()
        {
            if (_statusBarRoot == null)
            {
                Transform statusBar = transform.Find("FactoryProductionStatusBar");
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
        }

        private bool HasStatusBarReferences()
        {
            return _statusBarRoot != null &&
                   _trackRect != null &&
                   _fillRect != null &&
                   _fillImage != null;
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
    }
}
