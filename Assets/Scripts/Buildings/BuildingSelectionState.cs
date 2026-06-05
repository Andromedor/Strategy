using Strategy.Core;
using UnityEngine;

namespace Strategy.Buildings
{
    public class BuildingSelectionState : MonoBehaviour
    {
        [Header("Selection Visual")]
        [SerializeField] private GameObject _selectionVisual;
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.12f;
        [SerializeField, Min(0f)] private float _heightOffset = 0.08f;
        [SerializeField, Min(0.5f)] private float _minimumFootprintSize = 4f;
        [SerializeField] private Color _lineColor = new Color(0.3f, 0.92f, 1f, 0.95f);

        private LineRenderer _runtimeLine;
        private Material _runtimeMaterial;

        public bool IsSelected { get; private set; }

        private void Awake()
        {
            if (_selectionVisual != null)
            {
                _selectionVisual.SetActive(false);
                return;
            }

            CreateRuntimeVisual();
        }

        private void OnEnable()
        {
            EventManager.OnBuildingSelected += Select;
            EventManager.OnBuildingDeselected += Deselect;
            HideSelection();
        }

        private void OnDisable()
        {
            EventManager.OnBuildingSelected -= Select;
            EventManager.OnBuildingDeselected -= Deselect;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);
        }

        private void Select(GameObject building)
        {
            if (!Matches(building))
                return;

            IsSelected = true;
            ShowSelection();
        }

        private void Deselect(GameObject building)
        {
            if (!Matches(building))
                return;

            IsSelected = false;
            HideSelection();
        }

        private bool Matches(GameObject building)
        {
            if (building == null)
                return false;

            Transform selectedTransform = building.transform;
            return selectedTransform == transform ||
                   selectedTransform.IsChildOf(transform) ||
                   transform.IsChildOf(selectedTransform);
        }

        private void ShowSelection()
        {
            if (_selectionVisual != null)
            {
                _selectionVisual.SetActive(true);
                return;
            }

            if (_runtimeLine == null)
                CreateRuntimeVisual();

            if (_runtimeLine == null)
                return;

            UpdateRuntimeVisual();
            _runtimeLine.enabled = true;
        }

        private void HideSelection()
        {
            if (_selectionVisual != null)
                _selectionVisual.SetActive(false);

            if (_runtimeLine != null)
                _runtimeLine.enabled = false;
        }

        private void CreateRuntimeVisual()
        {
            GameObject visual = new GameObject("Building Selection Visual");
            visual.transform.SetParent(transform, false);
            visual.layer = gameObject.layer;

            _runtimeLine = visual.AddComponent<LineRenderer>();
            _runtimeLine.useWorldSpace = true;
            _runtimeLine.loop = true;
            _runtimeLine.positionCount = 4;
            _runtimeLine.startWidth = _lineWidth;
            _runtimeLine.endWidth = _lineWidth;
            _runtimeLine.numCapVertices = 2;
            _runtimeLine.numCornerVertices = 2;
            _runtimeLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _runtimeLine.receiveShadows = false;
            _runtimeLine.material = CreateLineMaterial();
            _runtimeLine.startColor = _lineColor;
            _runtimeLine.endColor = _lineColor;
            _runtimeLine.enabled = false;
        }

        private Material CreateLineMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");

            _runtimeMaterial = new Material(shader);

            if (_runtimeMaterial.HasProperty("_BaseColor"))
                _runtimeMaterial.SetColor("_BaseColor", _lineColor);

            if (_runtimeMaterial.HasProperty("_Color"))
                _runtimeMaterial.SetColor("_Color", _lineColor);

            return _runtimeMaterial;
        }

        private void UpdateRuntimeVisual()
        {
            Bounds bounds = ResolveBounds();
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float y = min.y + _heightOffset;

            _runtimeLine.SetPosition(0, new Vector3(min.x, y, min.z));
            _runtimeLine.SetPosition(1, new Vector3(max.x, y, min.z));
            _runtimeLine.SetPosition(2, new Vector3(max.x, y, max.z));
            _runtimeLine.SetPosition(3, new Vector3(min.x, y, max.z));
        }

        private Bounds ResolveBounds()
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(transform.position, Vector3.one * _minimumFootprintSize);

            Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer == _runtimeLine)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(collider.bounds);
            }

            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, _minimumFootprintSize);
            size.z = Mathf.Max(size.z, _minimumFootprintSize);
            bounds.size = size;
            return bounds;
        }
    }
}
