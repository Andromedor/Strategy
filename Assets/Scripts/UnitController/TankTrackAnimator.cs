using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Procedurally animates tank track segments by scrolling them around a looping path composed of
    /// two straight runs and two semicircular ends. Segment positions and pitches are recomputed every
    /// frame from the left/right track speeds provided by TrackedVehicleMotor (or NavMeshAgent velocity
    /// as fallback). All geometry is built at runtime — no prefab assets required.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class TankTrackAnimator : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private TrackedVehicleMotor _vehicleMotor;
        [SerializeField] private int _segmentsPerRun = 10;
        [SerializeField] private int _endSegmentsPerLoop = 6;
        [SerializeField] private float _trackHalfWidth = 1.58f;
        [SerializeField] private float _trackLength = 4.45f;
        [SerializeField] private float _trackCenterY = 0.42f;
        [SerializeField] private float _trackVerticalSpacing = 0.42f;
        [SerializeField] private Vector3 _segmentScale = new Vector3(0.24f, 0.075f, 0.34f);
        [SerializeField] private float _scrollSpeedMultiplier = 1.55f;
        [SerializeField] private float _maxVisualTrackSpeed = 8f;
        [SerializeField] private float _moveThreshold = 0.08f;
        [SerializeField] private Color _trackColor = new Color(0.075f, 0.07f, 0.06f, 1f);

        private readonly List<TrackSegment> _segments = new List<TrackSegment>();
        private Transform _trackRoot;
        private Material _trackMaterial;
        private Vector3 _lastPosition;
        private float _leftTrackOffset;
        private float _rightTrackOffset;
        private bool _hasLastPosition;
        private float _loopLength;

        private static Mesh _segmentMesh;

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            if (_vehicleMotor == null)
                _vehicleMotor = GetComponent<TrackedVehicleMotor>();

            BuildSegments();
        }

        private void OnValidate()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            if (_vehicleMotor == null)
                _vehicleMotor = GetComponent<TrackedVehicleMotor>();
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

            if (_loopLength <= 0f)
                return;

            GetTrackSpeeds(deltaTime, out float leftSpeed, out float rightSpeed);

            if (Mathf.Abs(leftSpeed) < _moveThreshold && Mathf.Abs(rightSpeed) < _moveThreshold)
                return;

            _leftTrackOffset = WrapOffset(_leftTrackOffset - leftSpeed * _scrollSpeedMultiplier * deltaTime);
            _rightTrackOffset = WrapOffset(_rightTrackOffset - rightSpeed * _scrollSpeedMultiplier * deltaTime);
            UpdateSegmentPositions();
        }

        /// <summary>
        /// Destroys the runtime track material to prevent memory leaks when this component is removed.
        /// </summary>
        private void OnDestroy()
        {
            if (_trackMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_trackMaterial);
            else
                DestroyImmediate(_trackMaterial);
        }

        /// <summary>
        /// Creates the track root object, shared material, shared segment mesh, and all TrackSegment
        /// entries for both the left and right loop.
        /// </summary>
        private void BuildSegments()
        {
            if (_segmentsPerRun <= 0)
                return;

            if (_trackRoot != null)
                return;

            _trackRoot = new GameObject("Track Motion Visuals").transform;
            _trackRoot.gameObject.layer = gameObject.layer;
            _trackRoot.SetParent(transform, false);
            _trackRoot.localPosition = Vector3.zero;
            _trackRoot.localRotation = Quaternion.identity;
            _trackRoot.localScale = Vector3.one;

            _trackMaterial = CreateTrackMaterial();
            Mesh mesh = GetSegmentMesh();
            _loopLength = CalculateLoopLength();
            int segmentCount = Mathf.Max(8, _segmentsPerRun * 2 + _endSegmentsPerLoop * 2);

            CreateLoop(-1f, segmentCount, mesh);
            CreateLoop(1f, segmentCount, mesh);

            UpdateSegmentPositions();
        }

        /// <summary>
        /// Instantiates segmentCount pad GameObjects evenly spaced around one track loop on the given
        /// side (-1 = left, +1 = right), each with a shared mesh and material.
        /// </summary>
        private void CreateLoop(float side, int segmentCount, Mesh mesh)
        {
            float spacing = _loopLength / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                GameObject segmentObject = new GameObject("Animated Track Pad");
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
                    i * spacing));
            }
        }

        /// <summary>
        /// Returns the vehicle velocity, preferring the NavMeshAgent's reported velocity when active,
        /// or computing it from position delta as a fallback for kinematically moved vehicles.
        /// </summary>
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

        /// <summary>
        /// Recomputes the local position and pitch angle of every segment based on its updated
        /// loop offset, placing left and right segments at +/- _trackHalfWidth.
        /// </summary>
        private void UpdateSegmentPositions()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                TrackSegment segment = _segments[i];
                float offset = segment.Side < 0f ? _leftTrackOffset : _rightTrackOffset;
                float distance = Mathf.Repeat(segment.BaseDistance + offset, _loopLength);
                TrackPose pose = EvaluateTrackPose(distance);

                segment.Transform.localPosition = new Vector3(segment.Side * _trackHalfWidth, pose.Y, pose.Z);
                segment.Transform.localRotation = Quaternion.Euler(pose.Pitch, 0f, 0f);
            }
        }

        /// <summary>
        /// Reads left and right track speeds from TrackedVehicleMotor when available; otherwise uses
        /// the forward component of velocity for both tracks equally.
        /// </summary>
        private void GetTrackSpeeds(float deltaTime, out float leftSpeed, out float rightSpeed)
        {
            if (_vehicleMotor != null && _vehicleMotor.enabled)
            {
                leftSpeed = ClampVisualSpeed(_vehicleMotor.LeftTrackSpeed);
                rightSpeed = ClampVisualSpeed(_vehicleMotor.RightTrackSpeed);
                return;
            }

            Vector3 velocity = GetVelocity(deltaTime);
            float forwardSpeed = transform.InverseTransformDirection(velocity).z;

            leftSpeed = ClampVisualSpeed(forwardSpeed);
            rightSpeed = ClampVisualSpeed(forwardSpeed);
        }

        /// <summary>
        /// Clamps speed to [-_maxVisualTrackSpeed, +_maxVisualTrackSpeed] to prevent runaway scroll.
        /// </summary>
        private float ClampVisualSpeed(float speed)
        {
            float maxSpeed = Mathf.Max(0.1f, _maxVisualTrackSpeed);
            return Mathf.Clamp(speed, -maxSpeed, maxSpeed);
        }

        /// <summary>
        /// Wraps the track scroll offset into [0, _loopLength] using Mathf.Repeat so it never drifts
        /// out of range.
        /// </summary>
        private float WrapOffset(float offset)
        {
            return Mathf.Repeat(offset, _loopLength);
        }

        /// <summary>
        /// Computes the total loop perimeter: two straight sections of _trackLength plus two
        /// semicircles of radius derived from _trackVerticalSpacing.
        /// </summary>
        private float CalculateLoopLength()
        {
            float radius = GetTrackRadius();
            return _trackLength * 2f + Mathf.PI * radius * 2f;
        }

        /// <summary>
        /// Maps a distance along the loop to a TrackPose (Y height, Z forward offset, pitch angle)
        /// by evaluating the four sections: top straight, front arc, bottom straight, rear arc.
        /// </summary>
        private TrackPose EvaluateTrackPose(float distance)
        {
            float radius = GetTrackRadius();
            float halfLength = _trackLength * 0.5f;
            float arcLength = Mathf.PI * radius;

            if (distance < _trackLength)
            {
                float z = -halfLength + distance;
                return new TrackPose(_trackCenterY + radius, z, 0f);
            }

            distance -= _trackLength;

            if (distance < arcLength)
            {
                float radians = Mathf.PI * 0.5f - distance / radius;
                float y = _trackCenterY + Mathf.Sin(radians) * radius;
                float z = halfLength + Mathf.Cos(radians) * radius;
                float pitch = -90f + distance / arcLength * 180f;
                return new TrackPose(y, z, pitch);
            }

            distance -= arcLength;

            if (distance < _trackLength)
            {
                float z = halfLength - distance;
                return new TrackPose(_trackCenterY - radius, z, 180f);
            }

            distance -= _trackLength;

            float rearRadians = -Mathf.PI * 0.5f - distance / radius;
            float rearY = _trackCenterY + Mathf.Sin(rearRadians) * radius;
            float rearZ = -halfLength + Mathf.Cos(rearRadians) * radius;
            float rearPitch = 90f + distance / arcLength * 180f;
            return new TrackPose(rearY, rearZ, rearPitch);
        }

        /// <summary>
        /// Returns the track sprocket radius (half of _trackVerticalSpacing, minimum 0.05).
        /// </summary>
        private float GetTrackRadius()
        {
            return Mathf.Max(0.05f, _trackVerticalSpacing * 0.5f);
        }

        /// <summary>
        /// Creates a unique runtime material for the track segments using the best available shader
        /// and the configured _trackColor.
        /// </summary>
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

        /// <summary>
        /// Returns the shared track-pad mesh, building it once from several AddBox calls and caching
        /// it in a static field. The Unity bool operator check guards against fake-null after domain reload.
        /// </summary>
        private static Mesh GetSegmentMesh()
        {
            if (_segmentMesh != null && _segmentMesh)
                return _segmentMesh;

            _segmentMesh = new Mesh
            {
                name = "Runtime Detailed Track Segment"
            };

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            AddBox(vertices, triangles, Vector3.zero, new Vector3(1f, 0.42f, 0.78f));
            AddBox(vertices, triangles, new Vector3(0f, 0.28f, -0.24f), new Vector3(0.92f, 0.18f, 0.12f));
            AddBox(vertices, triangles, new Vector3(0f, 0.28f, 0.24f), new Vector3(0.92f, 0.18f, 0.12f));
            AddBox(vertices, triangles, new Vector3(-0.52f, 0.02f, 0f), new Vector3(0.12f, 0.36f, 0.62f));
            AddBox(vertices, triangles, new Vector3(0.52f, 0.02f, 0f), new Vector3(0.12f, 0.36f, 0.62f));
            AddBox(vertices, triangles, new Vector3(0f, -0.18f, -0.39f), new Vector3(0.84f, 0.16f, 0.08f));
            AddBox(vertices, triangles, new Vector3(0f, -0.18f, 0.39f), new Vector3(0.84f, 0.16f, 0.08f));

            _segmentMesh.SetVertices(vertices);
            _segmentMesh.SetTriangles(triangles, 0);
            _segmentMesh.RecalculateNormals();
            _segmentMesh.RecalculateBounds();

            return _segmentMesh;
        }

        /// <summary>
        /// Appends 8 vertices and 12 triangles (6 faces) for an axis-aligned box to the mesh lists.
        /// </summary>
        private static void AddBox(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 size)
        {
            int start = vertices.Count;
            Vector3 half = size * 0.5f;

            vertices.Add(center + new Vector3(-half.x, -half.y, -half.z));
            vertices.Add(center + new Vector3(half.x, -half.y, -half.z));
            vertices.Add(center + new Vector3(half.x, half.y, -half.z));
            vertices.Add(center + new Vector3(-half.x, half.y, -half.z));
            vertices.Add(center + new Vector3(-half.x, -half.y, half.z));
            vertices.Add(center + new Vector3(half.x, -half.y, half.z));
            vertices.Add(center + new Vector3(half.x, half.y, half.z));
            vertices.Add(center + new Vector3(-half.x, half.y, half.z));

            AddFace(triangles, start, 0, 2, 1, 0, 3, 2);
            AddFace(triangles, start, 4, 5, 6, 4, 6, 7);
            AddFace(triangles, start, 0, 1, 5, 0, 5, 4);
            AddFace(triangles, start, 2, 3, 7, 2, 7, 6);
            AddFace(triangles, start, 1, 2, 6, 1, 6, 5);
            AddFace(triangles, start, 3, 0, 4, 3, 4, 7);
        }

        /// <summary>
        /// Appends a triangle fan of indices (offset by start) to the triangle list.
        /// </summary>
        private static void AddFace(List<int> triangles, int start, params int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
                triangles.Add(start + indices[i]);
        }

        /// <summary>
        /// Data container for a single track pad: its Transform, which side it belongs to (-1/+1),
        /// and its base distance along the loop used to compute its scrolling position.
        /// </summary>
        private readonly struct TrackSegment
        {
            public TrackSegment(Transform transform, float side, float baseDistance)
            {
                Transform = transform;
                Side = side;
                BaseDistance = baseDistance;
            }

            public Transform Transform { get; }
            public float Side { get; }
            public float BaseDistance { get; }
        }

        /// <summary>
        /// Immutable position/orientation result returned by EvaluateTrackPose for a single pad
        /// placement on the loop.
        /// </summary>
        private readonly struct TrackPose
        {
            public TrackPose(float y, float z, float pitch)
            {
                Y = y;
                Z = z;
                Pitch = pitch;
            }

            public float Y { get; }
            public float Z { get; }
            public float Pitch { get; }
        }
    }
}
