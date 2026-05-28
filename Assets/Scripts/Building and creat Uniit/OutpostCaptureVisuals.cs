using UnitController;
using UnityEngine;
using UnityEngine.Rendering;

namespace Building_and_creat_Uniit
{
    public class OutpostCaptureVisuals : MonoBehaviour
    {
        private const string BaseColorProperty = "_BaseColor";
        private const string ColorProperty = "_Color";

        [Header("References")]
        [SerializeField] private Outpost _outpost;
        [SerializeField] private Renderer _captureZoneRenderer;

        [Header("Progress Circle")]
        [SerializeField] private Vector3 _circleOffset = new Vector3(0f, 6f, -12f);
        [SerializeField, Min(0.1f)] private float _circleRadius = 2.4f;
        [SerializeField, Range(12, 128)] private int _circleSegments = 64;
        [SerializeField] private Color _circleBackgroundColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private float _fillLift = 0.03f;

        [Header("Capture Zone Blink")]
        [SerializeField, Min(0.1f)] private float _blinkSpeed = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _blinkMinAlpha = 0.18f;
        [SerializeField, Range(0f, 1f)] private float _blinkMaxAlpha = 0.48f;

        private GameObject _circleRoot;
        private MeshFilter _backgroundFilter;
        private MeshFilter _fillFilter;
        private MeshRenderer _backgroundRenderer;
        private MeshRenderer _fillRenderer;
        private Mesh _backgroundMesh;
        private Mesh _fillMesh;
        private Material _backgroundMaterial;
        private Material _fillMaterial;
        private MaterialPropertyBlock _zonePropertyBlock;
        private float _lastProgress = -1f;
        private TeamType? _lastCapturingTeam;

        private void Awake()
        {
            ResolveReferences();
            CreateCircle();
            HideCircle();
            RestoreCaptureZoneColor();
        }

        private void OnDisable()
        {
            HideCircle();
            RestoreCaptureZoneColor();
        }

        private void Update()
        {
            if (_outpost == null)
                return;

            if (!_outpost.IsBeingCaptured || _outpost.CapturingTeam == null)
            {
                HideCircle();
                RestoreCaptureZoneColor();
                return;
            }

            TeamType capturingTeam = _outpost.CapturingTeam.Value;

            ShowCircle();
            UpdateProgressCircle(_outpost.CaptureProgress, capturingTeam);
            BlinkCaptureZone(capturingTeam);
        }

        private void OnValidate()
        {
            _circleRadius = Mathf.Max(0.1f, _circleRadius);
            _circleSegments = Mathf.Clamp(_circleSegments, 12, 128);
            _blinkSpeed = Mathf.Max(0.1f, _blinkSpeed);
            _blinkMaxAlpha = Mathf.Max(_blinkMinAlpha, _blinkMaxAlpha);
        }

        private void ResolveReferences()
        {
            if (_outpost == null)
                _outpost = GetComponent<Outpost>();

            if (_captureZoneRenderer == null)
            {
                Transform captureZone = transform.Find("CaptureZone");

                if (captureZone != null)
                    _captureZoneRenderer = captureZone.GetComponent<Renderer>();
            }

            if (_zonePropertyBlock == null)
                _zonePropertyBlock = new MaterialPropertyBlock();
        }

        private void CreateCircle()
        {
            if (_circleRoot != null)
                return;

            _backgroundMaterial = CreateTransparentMaterial("Outpost Capture Circle Background");
            _fillMaterial = CreateTransparentMaterial("Outpost Capture Circle Fill");

            _circleRoot = new GameObject("CaptureProgressCircle");
            _circleRoot.transform.SetParent(transform, false);
            _circleRoot.transform.localPosition = _circleOffset;
            _circleRoot.transform.localRotation = Quaternion.identity;

            CreateCirclePart(
                "Background",
                0f,
                _circleBackgroundColor,
                _backgroundMaterial,
                out _backgroundFilter,
                out _backgroundRenderer,
                out _backgroundMesh);

            CreateCirclePart(
                "Fill",
                _fillLift,
                Color.white,
                _fillMaterial,
                out _fillFilter,
                out _fillRenderer,
                out _fillMesh);

            BuildDiscMesh(_backgroundMesh, 1f);
        }

        private void CreateCirclePart(
            string name,
            float lift,
            Color color,
            Material material,
            out MeshFilter meshFilter,
            out MeshRenderer meshRenderer,
            out Mesh mesh)
        {
            GameObject circlePart = new GameObject(name);
            circlePart.transform.SetParent(_circleRoot.transform, false);
            circlePart.transform.localPosition = new Vector3(0f, lift, 0f);

            meshFilter = circlePart.AddComponent<MeshFilter>();
            meshRenderer = circlePart.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = material;

            mesh = new Mesh { name = $"OutpostCapture{name}Mesh" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;

            SetMaterialColor(material, color);
        }

        private Material CreateTransparentMaterial(string materialName)
        {
            Shader shader =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color");

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3000
            };

            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", (float)CullMode.Off);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            return material;
        }

        private void UpdateProgressCircle(float progress, TeamType capturingTeam)
        {
            Color fillColor = _outpost.GetColorForTeam(capturingTeam);
            fillColor.a = 0.9f;

            SetMaterialColor(_fillMaterial, fillColor);

            if (!Mathf.Approximately(_lastProgress, progress) || _lastCapturingTeam != capturingTeam)
            {
                BuildDiscMesh(_fillMesh, progress);
                _lastProgress = progress;
                _lastCapturingTeam = capturingTeam;
            }
        }

        private void BuildDiscMesh(Mesh mesh, float progress)
        {
            mesh.Clear();

            progress = Mathf.Clamp01(progress);

            if (progress <= 0f)
                return;

            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(_circleSegments * progress));
            float sweepAngle = 360f * progress;

            Vector3[] vertices = new Vector3[segmentCount + 2];
            int[] triangles = new int[segmentCount * 3];

            vertices[0] = Vector3.zero;

            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = -90f + sweepAngle * i / segmentCount;
                float radians = angle * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(radians) * _circleRadius,
                    0f,
                    Mathf.Sin(radians) * _circleRadius);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 2;
                triangles[triangleIndex + 2] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private void BlinkCaptureZone(TeamType capturingTeam)
        {
            if (_captureZoneRenderer == null)
                return;

            float pulse = (Mathf.Sin(Time.time * _blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            Color color = _outpost.GetColorForTeam(capturingTeam);
            color.a = Mathf.Lerp(_blinkMinAlpha, _blinkMaxAlpha, pulse);

            SetRendererColor(_captureZoneRenderer, color);
        }

        private void RestoreCaptureZoneColor()
        {
            if (_outpost == null || _captureZoneRenderer == null)
                return;

            SetRendererColor(_captureZoneRenderer, _outpost.CurrentZoneColor);
            _lastProgress = -1f;
            _lastCapturingTeam = null;
        }

        private void ShowCircle()
        {
            if (_circleRoot != null && !_circleRoot.activeSelf)
                _circleRoot.SetActive(true);
        }

        private void HideCircle()
        {
            if (_circleRoot != null && _circleRoot.activeSelf)
                _circleRoot.SetActive(false);
        }

        private void SetRendererColor(Renderer targetRenderer, Color color)
        {
            if (_zonePropertyBlock == null)
                _zonePropertyBlock = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(_zonePropertyBlock);
            _zonePropertyBlock.SetColor(BaseColorProperty, color);
            _zonePropertyBlock.SetColor(ColorProperty, color);
            targetRenderer.SetPropertyBlock(_zonePropertyBlock);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty(BaseColorProperty))
                material.SetColor(BaseColorProperty, color);

            if (material.HasProperty(ColorProperty))
                material.SetColor(ColorProperty, color);

            material.color = color;
        }
    }
}
