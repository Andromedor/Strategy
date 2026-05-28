using DefaultNamespace;
using TMPro;
using UnitController;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class OutpostStatusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_Text _statusText;

        [Header("Layout")]
        [SerializeField] private Vector2 _anchoredPosition = new Vector2(-18f, -18f);
        [SerializeField] private Vector2 _textSize = new Vector2(320f, 132f);

        [Header("Style")]
        [SerializeField] private Color _textColor = new Color(0.04f, 0.07f, 0.1f, 1f);
        [SerializeField] private Color _backgroundColor = new Color(1f, 0.96f, 0.76f, 0.96f);
        [SerializeField] private Color _accentColor = new Color(0.04f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color _labelColor = new Color(0.22f, 0.27f, 0.3f, 1f);
        [SerializeField] private Color _moneyColor = new Color(0.55f, 0.29f, 0f, 1f);
        [SerializeField] private Color _incomeColor = new Color(0f, 0.38f, 0.12f, 1f);

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

        private void Refresh()
        {
            EnsureStatusText();

            if (_statusText == null)
                return;

            int playerResource = ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0;
            int capturedOutposts = Outpost.GetOwnedCount(TeamType.Player);
            int resourcePerMinute = Mathf.RoundToInt(Outpost.GetResourcePerMinute(TeamType.Player));
            int ticksPerMinute = Mathf.RoundToInt(Outpost.GetResourceTicksPerMinute(TeamType.Player));
            int secondsPerTick = ticksPerMinute > 0
                ? Mathf.RoundToInt(60f / ticksPerMinute)
                : 0;

            string generationFrequency = secondsPerTick > 0
                ? $"кожні {secondsPerTick} с"
                : "немає";

            _statusText.text =
                $"<size=20><b><color=#{ColorUtility.ToHtmlStringRGB(_accentColor)}>РЕСУРСИ</color></b></size>\n" +
                $"{Label("Зони")} {Value(capturedOutposts.ToString(), _textColor)}\n" +
                $"{Label("Гроші")} {Value(playerResource.ToString(), _moneyColor)}\n" +
                $"{Label("Дохід/хв")} {Value($"+{resourcePerMinute}", _incomeColor)}\n" +
                $"{Label("Нарахування")} {Value(generationFrequency, _textColor)}";
        }

        private void EnsureStatusText()
        {
            if (_statusText != null)
                return;

            if (_canvas == null)
                _canvas = FindFirstObjectByType<Canvas>();

            if (_canvas == null)
                return;

            GameObject panelObject = new GameObject(
                "OutpostStatusPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            panelObject.transform.SetParent(_canvas.transform, false);

            RectTransform panelRect = (RectTransform)panelObject.transform;
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = _anchoredPosition;
            panelRect.sizeDelta = _textSize;

            Image background = panelObject.GetComponent<Image>();
            background.color = _backgroundColor;
            background.raycastTarget = false;

            CreateAccent(panelObject.transform);
            CreateStatusText(panelObject.transform);
        }

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

        private void CreateStatusText(Transform parent)
        {
            GameObject textObject = new GameObject(
                "OutpostStatusText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.transform.SetParent(parent, false);

            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 10f);
            textRect.offsetMax = new Vector2(-12f, -10f);

            _statusText = textObject.GetComponent<TextMeshProUGUI>();
            _statusText.color = _textColor;
            _statusText.fontSize = 18f;
            _statusText.fontStyle = FontStyles.Bold;
            _statusText.lineSpacing = 1f;
            _statusText.alignment = TextAlignmentOptions.TopLeft;
            _statusText.raycastTarget = false;
            _statusText.richText = true;
        }

        private string Label(string text)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(_labelColor)}>{text}:</color>";
        }

        private static string Value(string text, Color color)
        {
            return $"<b><color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color></b>";
        }
    }
}
