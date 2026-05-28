using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UnitController
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class TankTrackAnimator : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private int _segmentsPerRun = 10;
        [SerializeField] private float _trackHalfWidth = 1.58f;
        [SerializeField] private float _trackLength = 4.45f;
        [SerializeField] private float _trackCenterY = 0.42f;
        [SerializeField] private float _trackVerticalSpacing = 0.42f;
        [SerializeField] private Vector3 _segmentScale = new Vector3(0.24f, 0.075f, 0.34f);
        [SerializeField] private float _scrollSpeedMultiplier = 1.55f;
        [SerializeField] private float _moveThreshold = 0.08f;
        [SerializeField] private Color _trackColor = new Color(0.075f, 0.07f, 0.06f, 1f);

        private readonly List<TrackSegment> _segments = new List<TrackSegment>();
        private Transform _trackRoot;
        private Material _trackMaterial;
        private Vector3 _lastPosition;
        private float _trackOffset;
        private bool _hasLastPosition;

        private static Mesh _segmentMesh;

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            BuildSegments();
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _hasLastPosition = true;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
                return;

            Vector3 velocity = GetVelocity(deltaTime);
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float speed = velocity.magnitude;

            if (speed < _moveThreshold)
                return;

            float direction = Mathf.Abs(localVelocity.z) > 0.02f
                ? Mathf.Sign(localVelocity.z)
                : 1f;

            _trackOffset = WrapOffset(_trackOffset - direction * speed * _scrollSpeedMultiplier * deltaTime);
            UpdateSegmentPositions();
        }

        private void OnDestroy()
        {
            if (_trackMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_trackMaterial);
            else
                DestroyImmediate(_trackMaterial);
        }

        private void BuildSegments()
        {
            if (_segmentsPerRun <= 0)
                return;

            _trackRoot = new GameObject("Track Motion Visuals").transform;
            _trackRoot.gameObject.layer = gameObject.layer;
            _trackRoot.SetParent(transform, false);
            _trackRoot.localPosition = Vector3.zero;
            _trackRoot.localRotation = Quaternion.identity;
            _trackRoot.localScale = Vector3.one;

            _trackMaterial = CreateTrackMaterial();
            Mesh mesh = GetSegmentMesh();
            float spacing = _trackLength / _segmentsPerRun;

            CreateRun(-1f, false, spacing, mesh);
            CreateRun(-1f, true, spacing, mesh);
            CreateRun(1f, false, spacing, mesh);
            CreateRun(1f, true, spacing, mesh);

            UpdateSegmentPositions();
        }

        private void CreateRun(float side, bool topRun, float spacing, Mesh mesh)
        {
            float startZ = -_trackLength * 0.5f + spacing * 0.5f;

            for (int i = 0; i < _segmentsPerRun; i++)
            {
                GameObject segmentObject = new GameObject(topRun ? "Track Top Segment" : "Track Bottom Segment");
                segmentObject.layer = gameObject.layer;
                segmentObject.transform.SetParent(_trackRoot, false);
                segmentObject.transform.localScale = _segmentScale;

                MeshFilter meshFilter = segmentObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;

                MeshRenderer meshRenderer = segmentObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _trackMaterial;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = true;

                _segments.Add(new TrackSegment(
                    segmentObject.transform,
                    side,
                    topRun,
                    startZ + i * spacing));
            }
        }

        private Vector3 GetVelocity(float deltaTime)
        {
            if (_agent != null && _agent.enabled)
            {
                _lastPosition = transform.position;
                _hasLastPosition = true;
                return _agent.velocity;
            }

            if (!_hasLastPosition)
            {
                _lastPosition = transform.position;
                _hasLastPosition = true;
                return Vector3.zero;
            }

            Vector3 velocity = (transform.position - _lastPosition) / deltaTime;
            _lastPosition = transform.position;
            return velocity;
        }

        private void UpdateSegmentPositions()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                TrackSegment segment = _segments[i];
                float runOffset = segment.IsTopRun ? -_trackOffset : _trackOffset;
                float z = WrapZ(segment.BaseZ + runOffset);
                float y = _trackCenterY + (segment.IsTopRun ? 0.5f : -0.5f) * _trackVerticalSpacing;

                segment.Transform.localPosition = new Vector3(segment.Side * _trackHalfWidth, y, z);
                segment.Transform.localRotation = Quaternion.identity;
            }
        }

        private float WrapOffset(float offset)
        {
            return Mathf.Repeat(offset, _trackLength);
        }

        private float WrapZ(float z)
        {
            return Mathf.Repeat(z + _trackLength * 0.5f, _trackLength) - _trackLength * 0.5f;
        }

        private Material CreateTrackMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Unlit/Color");

            Material material = new Material(shader);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", _trackColor);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", _trackColor);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.22f);

            return material;
        }

        private static Mesh GetSegmentMesh()
        {
            if (_segmentMesh != null)
                return _segmentMesh;

            _segmentMesh = new Mesh
            {
                name = "Runtime Track Segment"
            };

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f)
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                1, 2, 6, 1, 6, 5,
                3, 0, 4, 3, 4, 7
            };

            _segmentMesh.vertices = vertices;
            _segmentMesh.triangles = triangles;
            _segmentMesh.RecalculateNormals();
            _segmentMesh.RecalculateBounds();

            return _segmentMesh;
        }

        private readonly struct TrackSegment
        {
            public TrackSegment(Transform transform, float side, bool isTopRun, float baseZ)
            {
                Transform = transform;
                Side = side;
                IsTopRun = isTopRun;
                BaseZ = baseZ;
            }

            public Transform Transform { get; }
            public float Side { get; }
            public bool IsTopRun { get; }
            public float BaseZ { get; }
        }
    }
}
