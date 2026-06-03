using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
    /// <summary>
    /// Відображає один слот control group: номер клавіші та загальну кількість живих юнітів у групі.
    /// </summary>
    public class ControlGroupSlotUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _numberText;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _fallbackText;
        [SerializeField] private Image _background;
        [SerializeField] private Image _activeFrame;
        [SerializeField] private Color _emptyColor = new Color(0.05f, 0.08f, 0.1f, 0.45f);
        [SerializeField] private Color _assignedColor = new Color(0.02f, 0.36f, 0.58f, 0.92f);

        public void SetData(int groupNumber, int unitCount)
        {
            SetData(groupNumber, unitCount, null, string.Empty);
        }

        public void SetData(int groupNumber, int unitCount, Sprite icon, string fallbackText)
        {
            bool assigned = unitCount > 0;

            if (assigned && !gameObject.activeSelf)
                gameObject.SetActive(true);

            if (_numberText != null)
                _numberText.text = groupNumber.ToString();

            if (_countText != null)
            {
                _countText.text = assigned ? "x" + unitCount : string.Empty;
                _countText.gameObject.SetActive(assigned);
            }

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = assigned && icon != null;
            }

            if (_fallbackText != null)
            {
                _fallbackText.text = !string.IsNullOrWhiteSpace(fallbackText) ? fallbackText : "UNIT";
                _fallbackText.gameObject.SetActive(assigned && icon == null);
            }

            if (_background != null)
                _background.color = assigned ? _assignedColor : _emptyColor;

            if (_activeFrame != null)
                _activeFrame.gameObject.SetActive(assigned);

            if (!assigned && gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
