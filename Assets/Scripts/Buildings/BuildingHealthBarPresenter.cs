using Strategy.Core;
using Strategy.Units;
using UnityEngine;
using UnityEngine.Rendering;

namespace Strategy.Buildings
{
    [DisallowMultipleComponent]
    public class BuildingHealthBarPresenter : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SpecColorId = Shader.PropertyToID("_SpecColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        [SerializeField] private BuildingHealth _health;
        [SerializeField] private Transform _barRoot;
        [SerializeField] private Transform _fill;
        [SerializeField] private Renderer _trackRenderer;
        [SerializeField] private Renderer _fillRenderer;
        [SerializeField] private Color _trackColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color _fillColor = new Color(0.08f, 1f, 0.32f, 1f);
        [SerializeField] private Color _warningFillColor = new Color(1f, 0.82f, 0.08f, 1f);
        [SerializeField] private Color _criticalFillColor = new Color(1f, 0.14f, 0.09f, 1f);
        [SerializeField, Range(0f, 1f)] private float _warningHealthThreshold = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _criticalHealthThreshold = 0.25f;
        [SerializeField, Min(0.01f)] private float _colorResponse = 12f;
        [SerializeField] private int _trackSortingOrder = 500;
        [SerializeField] private int _fillSortingOrder = 501;
        [SerializeField] private int _rendererPriority = 100;
        [SerializeField, Min(0f)] private float _visibleAfterDamageSeconds = 5f;
        [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.8f;

        private MaterialPropertyBlock _trackPropertyBlock;
        private MaterialPropertyBlock _fillPropertyBlock;
        private Vector3 _fullFillScale = Vector3.one;
        private Vector3 _fullFillPosition;
        private float _lastDamageTime = float.NegativeInfinity;
        private float _currentAlpha;
        private float _lastFillAmount = 1f;
        private Color _displayFillColor;
        private bool _hasDisplayFillColor;
        private bool _isSelected;

        public bool IsVisible => _barRoot != null && _barRoot.gameObject.activeSelf;
        public float CurrentAlpha => _currentAlpha;
        public float FillAmount => _lastFillAmount;

#if UNITY_EDITOR
        private void OnValidate()
        {
            ConfigureRendererOrder();
        }
#endif

        private void Awake()
        {
            _trackPropertyBlock = new MaterialPropertyBlock();
            _fillPropertyBlock = new MaterialPropertyBlock();

            if (_health == null)
                _health = GetComponent<BuildingHealth>();

            ConfigureRendererOrder();

            if (_fill != null)
            {
                _fullFillScale = _fill.localScale;
                _fullFillPosition = _fill.localPosition;
            }

            ApplyHealthFill();
            SnapDisplayColorToTarget();
            ApplyVisibility(0f);
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.HealthChanged += OnHealthChanged;

            EventManager.OnBuildingSelected += OnBuildingSelected;
            EventManager.OnBuildingDeselected += OnBuildingDeselected;
            UnitHealthBarVisibility.ForceVisibilityChanged += OnForceVisibilityChanged;
            ApplyVisibility(ResolveTargetAlpha());
        }

        private void Start()
        {
            ApplyHealthFill();
            ApplyVisibility(ResolveTargetAlpha());
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.HealthChanged -= OnHealthChanged;

            EventManager.OnBuildingSelected -= OnBuildingSelected;
            EventManager.OnBuildingDeselected -= OnBuildingDeselected;
            UnitHealthBarVisibility.ForceVisibilityChanged -= OnForceVisibilityChanged;
        }

        private void Update()
        {
            UnitHealthBarVisibility.PollKeyboard();
            UpdateDisplayColor(Time.deltaTime);
            ApplyVisibility(ResolveTargetAlpha());
        }

        private void OnHealthChanged(BuildingHealth health)
        {
            _lastDamageTime = Time.time;
            ApplyHealthFill();
            ApplyVisibility(1f);
        }

        private void OnBuildingSelected(GameObject building)
        {
            if (building != gameObject)
                return;

            _isSelected = true;
            ApplyVisibility(ResolveTargetAlpha());
        }

        private void OnBuildingDeselected(GameObject building)
        {
            if (building != gameObject)
                return;

            _isSelected = false;
            ApplyVisibility(ResolveTargetAlpha());
        }

        private void OnForceVisibilityChanged(bool visible)
        {
            ApplyVisibility(ResolveTargetAlpha());
        }

        private float ResolveTargetAlpha()
        {
            if (_health == null || _health.MaxHealth <= 0f)
                return 0f;

            if (_isSelected || UnitHealthBarVisibility.ForceVisible)
                return 1f;

            float elapsedSinceDamage = Time.time - _lastDamageTime;
            if (elapsedSinceDamage <= _visibleAfterDamageSeconds)
                return 1f;

            return Mathf.Clamp01(1f - (elapsedSinceDamage - _visibleAfterDamageSeconds) / _fadeSeconds);
        }

        private void ApplyHealthFill()
        {
            if (_health == null || _fill == null)
                return;

            _lastFillAmount = _health.NormalizedHealth;

            Vector3 scale = _fullFillScale;
            scale.x = _fullFillScale.x * _lastFillAmount;
            _fill.localScale = scale;

            Vector3 position = _fullFillPosition;
            position.x -= (_fullFillScale.x - scale.x) * 0.5f;
            _fill.localPosition = position;
        }

        private void ApplyVisibility(float alpha)
        {
            _currentAlpha = Mathf.Clamp01(alpha);
            bool visible = _currentAlpha > 0.001f;

            if (_barRoot != null && _barRoot.gameObject.activeSelf != visible)
                _barRoot.gameObject.SetActive(visible);

            ApplyColor(_trackRenderer, _trackPropertyBlock, _trackColor, _currentAlpha);
            ApplyColor(_fillRenderer, _fillPropertyBlock, ResolveDisplayFillColor(), _currentAlpha);
        }

        private Color ResolveFillColor()
        {
            float warning = Mathf.Clamp01(_warningHealthThreshold);
            float critical = Mathf.Clamp01(_criticalHealthThreshold);

            if (warning < critical)
                (warning, critical) = (critical, warning);

            if (_lastFillAmount <= critical)
                return _criticalFillColor;

            if (_lastFillAmount <= warning)
            {
                float criticalToWarning = Mathf.InverseLerp(critical, warning, _lastFillAmount);
                return Color.Lerp(_criticalFillColor, _warningFillColor, Mathf.SmoothStep(0f, 1f, criticalToWarning));
            }

            float warningToHealthy = Mathf.InverseLerp(warning, 1f, _lastFillAmount);
            return Color.Lerp(_warningFillColor, _fillColor, Mathf.SmoothStep(0f, 1f, warningToHealthy));
        }

        private Color ResolveDisplayFillColor()
        {
            if (!_hasDisplayFillColor)
                SnapDisplayColorToTarget();

            return _displayFillColor;
        }

        private void SnapDisplayColorToTarget()
        {
            _displayFillColor = ResolveFillColor();
            _hasDisplayFillColor = true;
        }

        private void UpdateDisplayColor(float deltaTime)
        {
            Color targetColor = ResolveFillColor();

            if (!_hasDisplayFillColor || deltaTime <= 0f)
            {
                _displayFillColor = targetColor;
                _hasDisplayFillColor = true;
                return;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, _colorResponse) * deltaTime);
            _displayFillColor = Color.Lerp(_displayFillColor, targetColor, t);
        }

        private void ConfigureRendererOrder()
        {
            ConfigureRenderer(_trackRenderer, _trackSortingOrder);
            ConfigureRenderer(_fillRenderer, _fillSortingOrder);
        }

        private void ConfigureRenderer(Renderer target, int sortingOrder)
        {
            if (target == null)
                return;

            target.sortingOrder = sortingOrder;
            target.rendererPriority = _rendererPriority;
            target.shadowCastingMode = ShadowCastingMode.Off;
            target.receiveShadows = false;
        }

        private void ApplyColor(Renderer target, MaterialPropertyBlock propertyBlock, Color color, float alpha)
        {
            if (target == null)
                return;

            propertyBlock ??= new MaterialPropertyBlock();

            Color runtimeColor = color;
            runtimeColor.a *= alpha;

            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, runtimeColor);
            propertyBlock.SetColor(ColorId, runtimeColor);
            propertyBlock.SetColor(EmissionColorId, new Color(runtimeColor.r, runtimeColor.g, runtimeColor.b, runtimeColor.a));
            propertyBlock.SetColor(SpecColorId, Color.black);
            propertyBlock.SetFloat(SmoothnessId, 0f);
            target.SetPropertyBlock(propertyBlock);
        }
    }
}
