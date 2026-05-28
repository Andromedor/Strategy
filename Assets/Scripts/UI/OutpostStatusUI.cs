using DefaultNamespace;
using TMPro;
using UnitController;
using UnityEngine;

namespace UI
{
    public class OutpostStatusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_Text _statusText;

        [Header("Layout")]
        [SerializeField] private Vector2 _anchoredPosition = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 _textSize = new Vector2(260f, 120f);

        [Header("Style")]
        [SerializeField] private Color _textColor = Color.white;

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
                $"Зони: {capturedOutposts}\n" +
                $"Гроші: {playerResource}\n" +
                $"Дохід за хвилину: +{resourcePerMinute}\n" +
                $"Нарахування: {generationFrequency}";
        }

        private void EnsureStatusText()
        {
            if (_statusText != null)
                return;

            if (_canvas == null)
                _canvas = FindFirstObjectByType<Canvas>();

            if (_canvas == null)
                return;

            GameObject textObject = new GameObject(
                "OutpostStatusText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.transform.SetParent(_canvas.transform, false);

            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = new Vector2(1f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(1f, 1f);
            textRect.anchoredPosition = _anchoredPosition;
            textRect.sizeDelta = _textSize;

            _statusText = textObject.GetComponent<TextMeshProUGUI>();
            _statusText.color = _textColor;
            _statusText.fontSize = 20f;
            _statusText.fontStyle = FontStyles.Bold;
            _statusText.lineSpacing = 2f;
            _statusText.alignment = TextAlignmentOptions.TopRight;
            _statusText.raycastTarget = false;
        }
    }
}
