using DefaultNamespace;
using TMPro;
using UnitController;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class OutpostPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private TMP_Text _resourceText;
        [SerializeField] private TMP_FontAsset _fontAsset;

        private Outpost _currentOutpost;
        private TMP_Text _upgradeButtonText;

        private void OnEnable()
        {
            EventManager.OnOutpostSelected += Open;
            ResourceManager.OnResourceChanged += UpdateResourceText;
            CacheReferences();
            ApplyLayout();

            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(Upgrade);
        }

        private void OnDisable()
        {
            EventManager.OnOutpostSelected -= Open;
            ResourceManager.OnResourceChanged -= UpdateResourceText;

            if (_upgradeButton != null)
                _upgradeButton.onClick.RemoveListener(Upgrade);
        }

        private void Open(Outpost outpost)
        {
            if (outpost == null || outpost.Owner != TeamType.Player)
            {
                _currentOutpost = null;
                gameObject.SetActive(false);
                return;
            }

            _currentOutpost = outpost;

            gameObject.SetActive(true);
            ApplyLayout();
            Refresh();
        }

        private void Refresh()
        {
            if (_currentOutpost == null || _currentOutpost.Owner != TeamType.Player)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_costText != null)
            {
                _costText.text = _currentOutpost.IsUpgraded
                    ? "Outpost upgraded\nBuild area unlocked"
                    : $"Upgrade outpost\nCost: {_currentOutpost.UpgradeCost}";
            }

            UpdateResourceText(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
        }

        private void UpdateResourceText(int resource)
        {
            if (_resourceText != null)
                _resourceText.text = $"Resources: {resource}";

            RefreshButton();
        }

        private void RefreshButton()
        {
            if (_upgradeButton == null)
                return;

            _upgradeButton.interactable =
                _currentOutpost != null &&
                _currentOutpost.CanUpgrade;

            if (_upgradeButtonText == null)
                return;

            if (_currentOutpost == null)
            {
                _upgradeButtonText.text = "Upgrade";
                return;
            }

            if (_currentOutpost.IsUpgraded)
                _upgradeButtonText.text = "Upgraded";
            else if (!_currentOutpost.CanUpgrade)
                _upgradeButtonText.text = "Need resources";
            else
                _upgradeButtonText.text = "Upgrade Outpost";
        }

        private void Upgrade()
        {
            if (_currentOutpost == null)
                return;

            if (_currentOutpost.TryUpgrade())
                Refresh();
        }

        private void CacheReferences()
        {
            if (_upgradeButton != null && _upgradeButtonText == null)
                _upgradeButtonText = _upgradeButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void ApplyLayout()
        {
            RectTransform panelRect = transform as RectTransform;

            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }

            SetTextStyle(_costText, 18f, TextAlignmentOptions.Center);
            SetTextStyle(_resourceText, 16f, TextAlignmentOptions.Center);
            SetTextStyle(_upgradeButtonText, 16f, TextAlignmentOptions.Center);
            if (_upgradeButtonText != null)
                _upgradeButtonText.color = Color.white;

            SetRect(_costText != null ? _costText.rectTransform : null, new Vector2(0f, -26f), new Vector2(300f, 58f), true);
            SetRect(_resourceText != null ? _resourceText.rectTransform : null, new Vector2(0f, -90f), new Vector2(300f, 28f), true);

            if (_upgradeButton != null)
                SetRect(_upgradeButton.transform as RectTransform, new Vector2(0f, 38f), new Vector2(260f, 42f), false);

            if (_upgradeButtonText != null)
                SetRect(_upgradeButtonText.rectTransform, Vector2.zero, Vector2.zero, false, true);
        }

        private void SetTextStyle(TMP_Text text, float fontSize, TextAlignmentOptions alignment)
        {
            if (text == null)
                return;

            if (_fontAsset != null)
                text.font = _fontAsset;

            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.04f, 0.075f, 0.11f, 1f);
            text.raycastTarget = false;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size,
            bool top,
            bool stretch = false)
        {
            if (rectTransform == null)
                return;

            if (stretch)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                return;
            }

            rectTransform.anchorMin = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rectTransform.anchorMax = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rectTransform.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }
    }
}
