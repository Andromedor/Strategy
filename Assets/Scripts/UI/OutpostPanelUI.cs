using Strategy.Core;
using Strategy.Buildings;
using TMPro;
using Strategy.Units;
using UnityEngine;
using UnityEngine.UI;

using Strategy.Data;
using Strategy.UI;
namespace Strategy.UI
{
    /// <summary>
    /// Панель, що відображається, коли гравець вибирає <see cref="Outpost"/> гравця.
    /// Показує вартість покращення та поточні ресурси, і надає кнопку покращення, яка
    /// викликає <see cref="Outpost.TryUpgrade"/>. Приховується для нейтральних або ворожих аванпостів.
    /// </summary>
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

        /// <summary>
        /// Показує панель для вказаного аванпосту гравця або приховує її, якщо null або ворожий.
        /// Викликається на <see cref="EventManager.OnOutpostSelected"/>.
        /// </summary>
        private void Open(Outpost outpost)
        {
            if (outpost == null || outpost.Owner != LocalPlayerContext.LocalTeam)
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

        /// <summary>Оновлює мітку вартості/стану та оновлює стан кнопки покращення.</summary>
        private void Refresh()
        {
            if (_currentOutpost == null || _currentOutpost.Owner != LocalPlayerContext.LocalTeam)
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

        /// <summary>
        /// Оновлює мітку лічильника ресурсів та повторно оцінює доступність кнопки.
        /// Викликається на <see cref="ResourceManager.OnResourceChanged"/>.
        /// </summary>
        private void UpdateResourceText(int resource)
        {
            if (_resourceText != null)
                _resourceText.text = $"Resources: {resource}";

            RefreshButton();
        }

        /// <summary>
        /// Встановлює інтерактивність кнопки з <see cref="Outpost.CanUpgrade"/> та оновлює її
        /// текст мітки для відображення поточного стану покращення ("Upgrade", "Upgraded", "Need resources").
        /// </summary>
        private void RefreshButton()
        {
            if (_upgradeButton == null)
                return;

            _upgradeButton.interactable =
                _currentOutpost != null &&
                _currentOutpost.CanUpgradeFor(LocalPlayerContext.LocalTeam);

            if (_upgradeButtonText == null)
                return;

            if (_currentOutpost == null)
            {
                _upgradeButtonText.text = "Upgrade";
                return;
            }

            if (_currentOutpost.IsUpgraded)
                _upgradeButtonText.text = "Upgraded";
            else if (!_currentOutpost.CanUpgradeFor(LocalPlayerContext.LocalTeam))
                _upgradeButtonText.text = "Need resources";
            else
                _upgradeButtonText.text = "Upgrade Outpost";
        }

        /// <summary>Намагається покращити поточний аванпост та оновлює панель у разі успіху.</summary>
        private void Upgrade()
        {
            if (_currentOutpost == null)
                return;

            PlayerCommand command = PlayerCommand.UpgradeOutpost(
                LocalPlayerContext.LocalTeam,
                LocalPlayerContext.LocalPlayerId,
                _currentOutpost.transform);

            if (CommandDispatcher.Dispatch(command, ExecuteUpgradeCommand))
                Refresh();
        }

        private void ExecuteUpgradeCommand(PlayerCommand command)
        {
            Outpost outpost = command.TargetTransform != null
                ? command.TargetTransform.GetComponentInParent<Outpost>()
                : null;
            outpost?.TryUpgrade(command.Team);
        }

        /// <summary>Знаходить дочірню мітку TMP_Text кнопки покращення для подальшого використання.</summary>
        private void CacheReferences()
        {
            if (_upgradeButton != null && _upgradeButtonText == null)
                _upgradeButtonText = _upgradeButton.GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>Розтягує панель для заповнення батьківського об'єкта та позиціонує всі RectTransform піделементів.</summary>
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

        /// <summary>Застосовує налаштування шрифту, автопідбору розміру, вирівнювання та кольору до мітки TMP.</summary>
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

        /// <summary>
        /// Позиціонує RectTransform, прив'язаний до верхнього або нижнього центру батьківського об'єкта,
        /// або розтягує його для заповнення, якщо <paramref name="stretch"/> є true.
        /// </summary>
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
