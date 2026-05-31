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
    /// Процедурно анімує секції гусениць танка, прокручуючи їх по замкненому шляху, що складається
    /// з двох прямих ділянок та двох напівкіл. Позиції та кути нахилу секцій перераховуються щокадру
    /// зі швидкостей лівої/правої гусениць, що надає TrackedVehicleMotor (або швидкість NavMeshAgent
    /// як запасний варіант). Уся геометрія будується під час виконання — готові ресурси не потрібні.
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
        /// Знищує матеріал гусениць під час виконання, щоб запобігти витокам пам'яті при видаленні цього компонента.
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
        /// Створює кореневий об'єкт гусениць, спільний матеріал, спільний меш секцій та всі записи TrackSegment
        /// для лівого та правого замкненого контуру.
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
        /// Створює segmentCount пластин GameObject рівномірно розподілених по одному контуру гусениці на заданій
        /// стороні (-1 = ліво, +1 = право), кожна зі спільним мешем та матеріалом.
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
        /// Повертає швидкість транспортного засобу, надаючи перевагу повідомленій швидкості NavMeshAgent,
        /// або обчислює її з дельти позиції як запасний варіант для кінематично переміщуваних транспортних засобів.
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
        /// Перераховує локальну позицію та кут нахилу кожної секції на основі оновленого
        /// зміщення по контуру, розміщуючи ліві та праві секції на +/- _trackHalfWidth.
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
        /// Зчитує швидкості лівої та правої гусениць із TrackedVehicleMotor, якщо доступний; інакше
        /// використовує прямолінійну складову швидкості для обох гусениць однаково.
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
        /// Обмежує швидкість до [-_maxVisualTrackSpeed, +_maxVisualTrackSpeed], щоб запобігти нескінченному прокручуванню.
        /// </summary>
        private float ClampVisualSpeed(float speed)
        {
            float maxSpeed = Mathf.Max(0.1f, _maxVisualTrackSpeed);
            return Mathf.Clamp(speed, -maxSpeed, maxSpeed);
        }

        /// <summary>
        /// Обертає зміщення прокручування гусениці в [0, _loopLength] за допомогою Mathf.Repeat,
        /// щоб воно ніколи не виходило за межі діапазону.
        /// </summary>
        private float WrapOffset(float offset)
        {
            return Mathf.Repeat(offset, _loopLength);
        }

        /// <summary>
        /// Обчислює загальний периметр контуру: дві прямі ділянки _trackLength плюс два
        /// напівкола радіусу, отриманого з _trackVerticalSpacing.
        /// </summary>
        private float CalculateLoopLength()
        {
            float radius = GetTrackRadius();
            return _trackLength * 2f + Mathf.PI * radius * 2f;
        }

        /// <summary>
        /// Відображає відстань вздовж контуру на TrackPose (висота Y, зміщення Z вперед, кут нахилу)
        /// шляхом оцінки чотирьох ділянок: верхня пряма, передня дуга, нижня пряма, задня дуга.
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
        /// Повертає радіус зірочки гусениці (половина _trackVerticalSpacing, мінімум 0.05).
        /// </summary>
        private float GetTrackRadius()
        {
            return Mathf.Max(0.05f, _trackVerticalSpacing * 0.5f);
        }

        /// <summary>
        /// Створює унікальний матеріал під час виконання для секцій гусениці, використовуючи найкращий
        /// доступний шейдер та налаштований _trackColor.
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
        /// Повертає спільний меш пластини гусениці, будуючи його один раз із кількох викликів AddBox та
        /// кешуючи в статичному полі. Перевірка оператором bool Unity захищає від фіктивного null після перезавантаження домену.
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
        /// Додає 8 вершин та 12 трикутників (6 граней) для вісь-вирівняного паралелепіпеда до списків меша.
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
        /// Додає масив індексів (зміщених на start) до списку трикутників.
        /// </summary>
        private static void AddFace(List<int> triangles, int start, params int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
                triangles.Add(start + indices[i]);
        }

        /// <summary>
        /// Контейнер даних для однієї пластини гусениці: її Transform, до якої сторони вона належить (-1/+1),
        /// та її базова відстань вздовж контуру, що використовується для обчислення позиції прокручування.
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
        /// Незмінний результат позиції/орієнтації, що повертається EvaluateTrackPose для розміщення
        /// однієї пластини на контурі.
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
