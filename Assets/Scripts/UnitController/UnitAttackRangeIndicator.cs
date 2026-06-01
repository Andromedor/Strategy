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
    /// Малює круговий LineRenderer навколо юніта, що показує дальність атаки. Коло відображається
    /// автоматично, коли юніт вибраний (якщо _showWhileSelected = true), і може необов'язково
    /// перемикатися клавішею при виборі. Успадковується ArtilleryRangeIndicator.
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
        /// При кожному LateUpdate перемальовує коло лише тоді, коли радіус або позиція змінилися
        /// більше ніж на епсилон, уникаючи перебудови меша щокадру при нерухомому юніті.
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
        /// Реагує на глобальну подію вибору юніта; показує коло дальності для цього юніта.
        /// </summary>
        private void OnUnitSelected(GameObject unit)
        {
            if (unit != gameObject)
                return;

            _isSelected = true;
            ApplyVisibility();
        }

        /// <summary>
        /// Реагує на глобальну подію скасування вибору юніта; приховує коло дальності для цього юніта.
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
        /// Створює дочірній об'єкт LineRenderer та runtime-матеріал для кола дальності.
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
        /// Створює runtime unlit-матеріал для LineRenderer, використовуючи найкращий доступний шейдер.
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
        /// Визначає, чи має бути коло видимим на основі стану вибору, _showWhileSelected
        /// та стану перемикача клавіші; потім викликає Show або Hide.
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
        /// Вмикає LineRenderer та примусово виконує негайне перемалювання кола у поточній позиції та дальності.
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
        /// Вимикає LineRenderer, щоб коло більше не відображалось.
        /// </summary>
        private void Hide()
        {
            _isVisible = false;

            if (_line != null)
                _line.enabled = false;
        }

        /// <summary>
        /// Перераховує всі позиції LineRenderer для кола заданого радіуса з центром у center,
        /// піднятим на _heightOffset, та кешує радіус і позицію для перевірки змін.
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
