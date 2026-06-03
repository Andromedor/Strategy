using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
    /// <summary>
    /// Runtime-логіка однієї картки типу юніта у SelectionInfoPanel.
    /// Візуальна структура задається префабом, а компонент лише підставляє іконку, назву та кількість.
    /// </summary>
    public class SelectionUnitCardUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _fallbackText;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private TMP_Text _nameText;

        public void SetData(SelectionUnitCardViewModel model)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = model.Icon;
                _iconImage.enabled = model.Icon != null;
            }

            if (_fallbackText != null)
            {
                _fallbackText.text = model.FallbackText;
                _fallbackText.gameObject.SetActive(model.Icon == null);
            }

            if (_countText != null)
                _countText.text = model.Count.ToString();

            if (_nameText != null)
            {
                _nameText.text = model.DisplayName;
                _nameText.gameObject.SetActive(false);
            }
        }
    }

    public readonly struct SelectionUnitCardViewModel
    {
        public SelectionUnitCardViewModel(string displayName, Sprite icon, string fallbackText, int count)
        {
            DisplayName = displayName;
            Icon = icon;
            FallbackText = fallbackText;
            Count = Mathf.Max(0, count);
        }

        public string DisplayName { get; }
        public Sprite Icon { get; }
        public string FallbackText { get; }
        public int Count { get; }
    }
}
