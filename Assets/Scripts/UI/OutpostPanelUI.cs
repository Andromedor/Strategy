using DefaultNamespace;
using TMPro;
using UnitController;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class OutpostPanelUI: MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private TMP_Text _resourceText;

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
                if (_currentOutpost.IsUpgraded)
                {
                    _costText.text =
                        "Аванпост покращено\n" +
                        "Будівництво доступне";
                }
                else
                {
                    _costText.text =
                        "Покращення аванпоста\n" +
                        $"Ціна: {_currentOutpost.UpgradeCost} грошей";
                }
            }

            UpdateResourceText(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
        }

        private void UpdateResourceText(int resource)
        {
            if (_resourceText != null)
                _resourceText.text = $"Ваші гроші: {resource}";

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
                _upgradeButtonText.text = "Покращити";
                return;
            }

            if (_currentOutpost.IsUpgraded)
                _upgradeButtonText.text = "Вже покращено";
            else if (!_currentOutpost.CanUpgrade)
                _upgradeButtonText.text = "Недостатньо грошей";
            else
                _upgradeButtonText.text = "Покращити аванпост";
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
                panelRect.sizeDelta = new Vector2(360f, 150f);

            SetTextStyle(_costText, 18f, TextAlignmentOptions.Center);
            SetTextStyle(_resourceText, 17f, TextAlignmentOptions.Center);
            SetTextStyle(_upgradeButtonText, 18f, TextAlignmentOptions.Center);

            SetRect(_costText != null ? _costText.rectTransform : null, new Vector2(0f, 42f), new Vector2(330f, 58f));
            SetRect(_resourceText != null ? _resourceText.rectTransform : null, new Vector2(0f, 0f), new Vector2(330f, 28f));

            if (_upgradeButton != null)
                SetRect(_upgradeButton.transform as RectTransform, new Vector2(0f, -50f), new Vector2(260f, 38f));

            if (_upgradeButtonText != null)
                SetRect(_upgradeButtonText.rectTransform, Vector2.zero, Vector2.zero, true);
        }

        private static void SetTextStyle(TMP_Text text, float fontSize, TextAlignmentOptions alignment)
        {
            if (text == null)
                return;

            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size,
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

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }
    }
}
