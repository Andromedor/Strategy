using UnityEngine;
using UnityEngine.InputSystem;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Draws a circular LineRenderer around the unit showing its attack range. The circle is shown
    /// automatically when the unit is selected (if _showWhileSelected is true) and can optionally
    /// be toggled on/off with a configurable key while selected. Subclassed by ArtilleryRangeIndicator.
    /// </summary>
    public class UnitAttackRangeIndicator : MonoBehaviour
    {
        [SerializeField] private UnitCombat _combat;
        [SerializeField] private bool _showWhileSelected = true;
        [SerializeField] private bool _toggleWithKey;
        [SerializeField] private Key _toggleKey = Key.Z;
        [SerializeField, Range(32, 256)] private int _segments = 160;
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.16f;
        [SerializeField] private float _heightOffset = 0.08f;
        [SerializeField] private Color _lineColor = new Color(0.14f, 0.85f, 1f, 0.95f);

        private LineRenderer _line;
        private Material _material;
        private bool _isSelected;
        private bool _keyToggled;
        private bool _isVisible;
        private float _lastRadius = -1f;
        private Vector3 _lastPosition;

        private void Awake()
        {
            if (_combat == null)
                _combat = GetComponent<UnitCombat>();

            CreateLine();
            Hide();
        }

        private void OnEnable()
        {
            EventManager.OnUnitSelected += OnUnitSelected;
            EventManager.OnUnitDeselected += OnUnitDeselected;
        }

        private void OnDisable()
        {
            EventManager.OnUnitSelected -= OnUnitSelected;
            EventManager.OnUnitDeselected -= OnUnitDeselected;
        }

        private void OnDestroy()
        {
            if (_material == null)
                return;

            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);
        }

        private void Update()
        {
            if (!_isSelected || !_toggleWithKey || Keyboard.current == null ||
                !Keyboard.current[_toggleKey].wasPressedThisFrame)
            {
                return;
            }

            _keyToggled = !_keyToggled;
            ApplyVisibility();
        }

        /// <summary>
        /// Each late update, redraws the circle only when the radius or position has changed
        /// by more than epsilon, avoiding per-frame mesh rebuilds when stationary.
        /// </summary>
        private void LateUpdate()
        {
            if (!_isVisible || _line == null || _combat == null)
                return;

            float radius = _combat.AttackRange;
            Vector3 position = transform.position;

            if (Mathf.Abs(radius - _lastRadius) <= 0.01f && (position - _lastPosition).sqrMagnitude <= 0.0025f)
                return;

            UpdateCircle(position, radius);
        }

        /// <summary>
        /// Responds to the global unit-selected event; shows the range circle for this unit.
        /// </summary>
        private void OnUnitSelected(GameObject unit)
        {
            if (unit != gameObject)
                return;

            _isSelected = true;
            ApplyVisibility();
        }

        /// <summary>
        /// Responds to the global unit-deselected event; hides the range circle for this unit.
        /// </summary>
        private void OnUnitDeselected(GameObject unit)
        {
            if (unit != gameObject)
                return;

            _isSelected = false;
            _keyToggled = false;
            Hide();
        }

        /// <summary>
        /// Instantiates the LineRenderer child object and its runtime material for the range circle.
        /// </summary>
        private void CreateLine()
        {
            GameObject lineObject = new GameObject("Attack Range Circle");
            lineObject.transform.SetParent(transform, false);
            lineObject.layer = gameObject.layer;

            _line = lineObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = Mathf.Max(32, _segments);
            _line.startWidth = _lineWidth;
            _line.endWidth = _lineWidth;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 4;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            _material = CreateLineMaterial();
            _line.material = _material;
            _line.startColor = _lineColor;
            _line.endColor = _lineColor;
        }

        /// <summary>
        /// Creates a runtime unlit material for the LineRenderer using the best available shader.
        /// </summary>
        private Material CreateLineMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");

            Material material = new Material(shader);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", _lineColor);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", _lineColor);

            return material;
        }

        /// <summary>
        /// Determines whether the circle should be visible based on selection state, _showWhileSelected,
        /// and key-toggle state; then calls Show or Hide.
        /// </summary>
        private void ApplyVisibility()
        {
            if (!_isSelected || _combat == null)
            {
                Hide();
                return;
            }

            if (_showWhileSelected || _keyToggled)
                Show();
            else
                Hide();
        }

        /// <summary>
        /// Enables the LineRenderer and forces an immediate circle redraw at the current position and range.
        /// </summary>
        private void Show()
        {
            if (_line == null || _combat == null)
                return;

            _isVisible = true;
            _line.enabled = true;
            UpdateCircle(transform.position, _combat.AttackRange);
        }

        /// <summary>
        /// Disables the LineRenderer so the circle is no longer rendered.
        /// </summary>
        private void Hide()
        {
            _isVisible = false;

            if (_line != null)
                _line.enabled = false;
        }

        /// <summary>
        /// Recomputes all LineRenderer positions for a circle of the given radius centred at center,
        /// elevated by _heightOffset, and caches radius and position for dirty-checking.
        /// </summary>
        private void UpdateCircle(Vector3 center, float radius)
        {
            if (radius <= 0f)
            {
                Hide();
                return;
            }

            int segments = Mathf.Max(32, _segments);
            _line.positionCount = segments;

            float y = center.y + _heightOffset;

            for (int i = 0; i < segments; i++)
            {
                float radians = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 point = new Vector3(
                    center.x + Mathf.Cos(radians) * radius,
                    y,
                    center.z + Mathf.Sin(radians) * radius);

                _line.SetPosition(i, point);
            }

            _lastRadius = radius;
            _lastPosition = center;
        }
    }
}
