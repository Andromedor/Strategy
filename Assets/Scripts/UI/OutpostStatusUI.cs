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
    /// Top-bar HUD widget that shows the player's current money, number of captured outpost zones,
    /// and resource income per minute. Refreshes on <see cref="ResourceManager.OnResourceChanged"/>
    /// and <see cref="Outpost.OnStatsChanged"/> using rich-text color formatting.
    /// </summary>
    public class OutpostStatusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_FontAsset _fontAsset;

        [Header("Layout")]
        [SerializeField] private Vector2 _anchoredPosition = new Vector2(-18f, -18f);
        [SerializeField] private Vector2 _textSize = new Vector2(320f, 96f);

        [Header("Style")]
        [SerializeField] private Color _textColor = new Color(0.04f, 0.075f, 0.11f, 1f);
        [SerializeField] private Color _backgroundColor = new Color(0.95f, 0.98f, 1f, 0.94f);
        [SerializeField] private Color _accentColor = new Color(0.03f, 0.38f, 0.75f, 1f);
        [SerializeField] private Color _labelColor = new Color(0.24f, 0.36f, 0.46f, 1f);
        [SerializeField] private Color _moneyColor = new Color(0.58f, 0.34f, 0.02f, 1f);
        [SerializeField] private Color _incomeColor = new Color(0.05f, 0.45f, 0.16f, 1f);

        private void OnEnable()
        {
            ResourceManager.OnResourceChanged += OnResourceChanged;
            Outpost.OnStatsChanged += Refresh;
        }

        private void OnDisable()
        {
            ResourceManager.OnResourceChanged -= OnResourceChanged;
            Outpost.OnStatsChanged -= Refresh;
        }

        private void Start()
        {
            EnsureStatusText();
            Refresh();
        }

        private void OnResourceChanged(int resource)
        {
            Refresh();
        }

        /// <summary>
        /// Queries <see cref="ResourceManager"/> and <see cref="Outpost"/> aggregates, then
        /// rebuilds the rich-text status string showing zones, money, and income per minute.
        /// </summary>
        private void Refresh()
        {
            EnsureStatusText();

            if (_statusText == null)
                return;

            int playerResource = ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0;
            int capturedOutposts = Outpost.GetOwnedCount(TeamType.Player);
            int resourcePerMinute = Mathf.RoundToInt(Outpost.GetResourcePerMinute(TeamType.Player));

            _statusText.text =
                $"<b><color=#{ColorUtility.ToHtmlStringRGB(_accentColor)}>RESOURCES</color></b>  " +
                $"{Label("Zones")} {Value(capturedOutposts.ToString(), _textColor)}   " +
                $"{Label("Money")} {Value(playerResource.ToString(), _moneyColor)}   " +
                $"{Label("Income/min")} {Value("+" + resourcePerMinute, _incomeColor)}";
        }

        /// <summary>Applies font, sizing, and style settings to the status TMP_Text element.</summary>
        private void EnsureStatusText()
        {
            if (_statusText == null)
                return;

            if (_fontAsset != null)
                _statusText.font = _fontAsset;

            _statusText.color = _textColor;
            _statusText.fontSize = 28f;
            _statusText.fontStyle = FontStyles.Bold;
            _statusText.enableAutoSizing = true;
            _statusText.fontSizeMin = 18f;
            _statusText.fontSizeMax = 28f;
            _statusText.alignment = TextAlignmentOptions.MidlineLeft;
            _statusText.overflowMode = TextOverflowModes.Ellipsis;
            _statusText.raycastTarget = false;
            _statusText.richText = true;
        }

        /// <summary>Creates a narrow vertical accent bar on the left edge of the status panel for visual styling.</summary>
        private void CreateAccent(Transform parent)
        {
            GameObject accentObject = new GameObject(
                "Accent",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            accentObject.transform.SetParent(parent, false);

            RectTransform accentRect = (RectTransform)accentObject.transform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(5f, 0f);

            Image accent = accentObject.GetComponent<Image>();
            accent.color = _accentColor;
            accent.raycastTarget = false;
        }

        /// <summary>Procedurally creates the TMP_Text child that displays the resource status line.</summary>
        private void CreateStatusText(Transform parent)
        {
            GameObject textObject = new GameObject(
                "ResourceText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.transform.SetParent(parent, false);

            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);

            _statusText = textObject.GetComponent<TextMeshProUGUI>();
            if (_fontAsset != null)
                _statusText.font = _fontAsset;

            _statusText.color = _textColor;
            _statusText.fontSize = 18f;
            _statusText.fontStyle = FontStyles.Bold;
            _statusText.enableAutoSizing = true;
            _statusText.fontSizeMin = 12f;
            _statusText.fontSizeMax = 18f;
            _statusText.alignment = TextAlignmentOptions.MidlineLeft;
            _statusText.raycastTarget = false;
            _statusText.richText = true;
        }

        /// <summary>Wraps text in a color tag using the configured label color for dimmed field names.</summary>
        private string Label(string text)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(_labelColor)}>{text}:</color>";
        }

        /// <summary>Wraps text in bold and a caller-specified color tag for highlighted numeric values.</summary>
        private static string Value(string text, Color color)
        {
            return $"<b><color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color></b>";
        }
    }
}
