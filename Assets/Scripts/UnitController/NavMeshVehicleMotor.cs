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
    /// Драйвер колісного транспортного засобу за моделлю велосипеда, що перебирає на себе керування NavMeshAgent.
    /// Вимикає обертання агента, зчитує його бажану швидкість для визначення напрямку, потім безпосередньо
    /// обертає та переміщує GameObject. Керує прямою швидкістю, паузами вирівнювання та плавним кутом повороту.
    /// Успадковується TrackedVehicleMotor для диференціальних швидкостей гусениць.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NavMeshVehicleMotor : MonoBehaviour
    {
        [SerializeField] protected NavMeshAgent _agent;
        [SerializeField] protected bool _applyAgentTuning = true;
        [SerializeField] protected float _cruiseSpeed = 5.2f;
        [SerializeField] protected float _acceleration = 5.2f;
        [SerializeField] protected float _stoppingDistance = 1.45f;
        [SerializeField] protected float _maxSteerAngle = 28f;
        [SerializeField] protected float _steerResponsiveness = 7f;
        [SerializeField] protected float _maxSteerSpeed = 125f;
        [SerializeField] protected float _bodyTurnSpeed = 150f;
        [SerializeField] protected float _sharpTurnSlowdownAngle = 95f;
        [SerializeField] protected float _minimumForwardSpeed = 0f;
        [SerializeField] protected float _alignmentStopAngle = 105f;
        [SerializeField] protected float _alignmentResumeAngle = 68f;

        protected Vector3 _lastPosition;
        protected Vector3 _currentVelocity;
        protected float _currentSpeed;
        protected float _currentSteerAngle;
        protected bool _waitingForAlignment;
        private UnitTrafficCoordinator _trafficCoordinator;

        public float CurrentSpeed => _currentSpeed;
        public float CurrentSteerAngle => _currentSteerAngle;
        public Vector3 CurrentVelocity => _currentVelocity;

        protected NavMeshAgent Agent => _agent;
        protected float CruiseSpeed => _cruiseSpeed;
        protected float AlignmentResumeAngle => _alignmentResumeAngle;
        protected bool WaitingForAlignment => _waitingForAlignment;

        protected virtual void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            _trafficCoordinator = UnitTrafficCoordinator.Ensure(gameObject);
            ApplyAgentTuning();
        }

        protected virtual void OnEnable()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            if (_trafficCoordinator == null)
                _trafficCoordinator = UnitTrafficCoordinator.Ensure(gameObject);

            _lastPosition = transform.position;
            _currentVelocity = Vector3.zero;
            _currentSpeed = 0f;
            _currentSteerAngle = 0f;
            _waitingForAlignment = false;

            ApplyAgentTuning();
        }

        protected virtual void OnDisable()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        protected virtual void OnValidate()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            ApplyAgentTuning();
        }

        protected virtual void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
                return;

            UpdateMeasuredVelocity(deltaTime);

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                UpdateSteering(Vector3.zero, deltaTime);
                OnMotorUpdated(Vector3.zero, false, 0f, deltaTime);
                return;
            }

            Vector3 headingDirection = GetHeadingDirection();
            bool hasHeading = headingDirection.sqrMagnitude > 0.001f;
            float signedHeadingAngle = hasHeading
                ? Vector3.SignedAngle(transform.forward, headingDirection, Vector3.up)
                : 0f;
            float headingAngle = Mathf.Abs(signedHeadingAngle);

            UpdateAgentSpeed(headingAngle, hasHeading);
            UpdateBodyRotation(headingDirection, deltaTime);
            UpdateSteering(hasHeading ? headingDirection : Vector3.zero, deltaTime);
            OnMotorUpdated(
                hasHeading ? headingDirection : Vector3.zero,
                hasHeading,
                signedHeadingAngle,
                deltaTime);
        }

        /// <summary>
        /// Записує параметри мотора (швидкість, прискорення, обертання) до NavMeshAgent, щоб він
        /// відповідав налаштуванням транспортного засобу. Пропускає, якщо _applyAgentTuning вимкнено.
        /// </summary>
        protected virtual void ApplyAgentTuning()
        {
            if (!_applyAgentTuning || _agent == null)
                return;

            _agent.updatePosition = true;
            _agent.updateRotation = false;
            _agent.speed = _cruiseSpeed;
            _agent.acceleration = _acceleration;
            _agent.angularSpeed = 360f;
            _agent.stoppingDistance = _stoppingDistance;
            _agent.autoBraking = true;
        }

        /// <summary>
        /// Обчислює фактичну швидкість із дельти позиції між кадрами; повертається до
        /// agent.velocity, коли переміщення незначне (наприклад, перший кадр або стоїть).
        /// </summary>
        protected virtual void UpdateMeasuredVelocity(float deltaTime)
        {
            Vector3 positionDelta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            positionDelta.y = 0f;

            _currentVelocity = positionDelta / Mathf.Max(0.0001f, deltaTime);

            if (_currentVelocity.sqrMagnitude <= 0.0001f &&
                _agent != null &&
                _agent.enabled &&
                _agent.isOnNavMesh)
            {
                _currentVelocity = _agent.velocity;
                _currentVelocity.y = 0f;
            }

            _currentSpeed = Vector3.Dot(_currentVelocity, transform.forward);
        }

        /// <summary>
        /// Отримує нормалізований напрямок руху з desiredVelocity агента, потім steeringTarget,
        /// потім destination як послідовні запасні варіанти.
        /// </summary>
        protected virtual Vector3 GetHeadingDirection()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return Vector3.zero;

            Vector3 desiredVelocity = _agent.desiredVelocity;
            desiredVelocity.y = 0f;

            if (desiredVelocity.sqrMagnitude > 0.01f)
                return desiredVelocity.normalized;

            if (_agent.hasPath && !_agent.pathPending)
            {
                Vector3 steeringDirection = _agent.steeringTarget - transform.position;
                steeringDirection.y = 0f;

                if (steeringDirection.sqrMagnitude > 0.01f)
                    return steeringDirection.normalized;
            }

            if (_agent.hasPath || _agent.pathPending)
            {
                Vector3 destinationDirection = _agent.destination - transform.position;
                destinationDirection.y = 0f;

                if (destinationDirection.sqrMagnitude > 0.01f)
                    return destinationDirection.normalized;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Регулює agent.speed залежно від кута курсу; зупиняє рух (_waitingForAlignment = true),
        /// коли необхідний поворот перевищує _alignmentStopAngle, та відновлює нижче _alignmentResumeAngle.
        /// </summary>
        protected virtual void UpdateAgentSpeed(float headingAngle, bool hasHeading)
        {
            if (!hasHeading || !_agent.hasPath || _agent.pathPending)
            {
                _waitingForAlignment = false;
                _agent.isStopped = false;
                _agent.speed = _cruiseSpeed;
                return;
            }

            if (_agent.remainingDistance <= _agent.stoppingDistance + 0.05f)
            {
                _waitingForAlignment = false;
                _agent.isStopped = false;
                _agent.speed = _cruiseSpeed;
                return;
            }

            if (_waitingForAlignment)
                _waitingForAlignment = headingAngle > _alignmentResumeAngle;
            else if (headingAngle >= _alignmentStopAngle)
                _waitingForAlignment = true;

            if (_waitingForAlignment)
            {
                _agent.isStopped = true;
                return;
            }

            float turnT = Mathf.InverseLerp(0f, Mathf.Max(1f, _sharpTurnSlowdownAngle), headingAngle);
            _agent.isStopped = false;
            _agent.speed = Mathf.Lerp(_cruiseSpeed, _minimumForwardSpeed, turnT);
        }

        /// <summary>
        /// Обертає корпус транспортного засобу до headingDirection зі швидкістю _bodyTurnSpeed градусів на секунду.
        /// </summary>
        protected virtual void UpdateBodyRotation(Vector3 headingDirection, float deltaTime)
        {
            if (headingDirection.sqrMagnitude <= 0.001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(headingDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                Mathf.Max(1f, _bodyTurnSpeed) * deltaTime);
        }

        /// <summary>
        /// Плавно переміщує _currentSteerAngle до знакового кута курсу, обмеженого _maxSteerAngle,
        /// використовуючи експоненційне змішування разом з обмеженням максимальної швидкості.
        /// </summary>
        protected virtual void UpdateSteering(Vector3 headingDirection, float deltaTime)
        {
            float targetSteerAngle = 0f;

            if (headingDirection.sqrMagnitude > 0.001f)
            {
                float signedHeadingAngle =
                    Vector3.SignedAngle(transform.forward, headingDirection.normalized, Vector3.up);
                targetSteerAngle = Mathf.Clamp(signedHeadingAngle, -_maxSteerAngle, _maxSteerAngle);
            }

            float blend = 1f - Mathf.Exp(-_steerResponsiveness * deltaTime);
            float blendedTarget = Mathf.LerpAngle(_currentSteerAngle, targetSteerAngle, blend);
            _currentSteerAngle = Mathf.MoveTowardsAngle(
                _currentSteerAngle,
                blendedTarget,
                Mathf.Max(1f, _maxSteerSpeed) * deltaTime);
        }

        /// <summary>
        /// Точка розширення, що викликається щокадру після оновлення всіх станів мотора.
        /// Перевизначається в підкласах (наприклад, TrackedVehicleMotor) для обчислення швидкостей
        /// кожної гусениці або інших похідних даних без дублювання базової логіки Update.
        /// </summary>
        protected virtual void OnMotorUpdated(
            Vector3 headingDirection,
            bool hasHeading,
            float signedHeadingAngle,
            float deltaTime)
        {
        }
    }
}
