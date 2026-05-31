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
    /// Animates the wheels of a wheeled vehicle: steerable front wheels yaw to match the current
    /// steer angle from NavMeshVehicleMotor (or the agent), and all drive wheels spin proportionally
    /// to forward speed. Pivot GameObjects are created at runtime around each wheel transform.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class WheeledVehicleAnimator : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private NavMeshVehicleMotor _vehicleMotor;
        [SerializeField] private Transform[] _driveWheels;
        [SerializeField] private Transform[] _steeringWheels;
        [SerializeField] private float _wheelRadius = 0.42f;
        [SerializeField] private float _maxSteerAngle = 24f;
        [SerializeField] private float _steerResponsiveness = 5.2f;
        [SerializeField] private float _maxSteerSpeed = 85f;
        [SerializeField] private float _spinMultiplier = 1.08f;
        [SerializeField] private float _spinDirection = -1f;
        [SerializeField] private float _steerDirection = 1f;
        [SerializeField] private float _moveThreshold = 0.05f;

        private readonly List<WheelVisual> _wheels = new List<WheelVisual>();
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private float _spinAngle;
        private float _currentSteerAngle;

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            if (_vehicleMotor == null)
                _vehicleMotor = GetComponent<NavMeshVehicleMotor>();

            BuildWheelVisuals();
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _hasLastPosition = true;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f || _wheels.Count == 0)
                return;

            Vector3 velocity = GetVelocity(deltaTime);
            UpdateSteering(GetTargetSteerAngle(velocity), deltaTime);
            UpdateSpin(velocity, deltaTime);
        }

        /// <summary>
        /// Creates a WheelVisual for every unique wheel transform in _driveWheels and _steeringWheels,
        /// inserting a pivot GameObject between each wheel and its original parent for steer rotation.
        /// </summary>
        private void BuildWheelVisuals()
        {
            _wheels.Clear();

            HashSet<Transform> steeringSet = new HashSet<Transform>();

            if (_steeringWheels != null)
            {
                for (int i = 0; i < _steeringWheels.Length; i++)
                {
                    if (_steeringWheels[i] != null)
                        steeringSet.Add(_steeringWheels[i]);
                }
            }

            HashSet<Transform> added = new HashSet<Transform>();
            AddWheelSet(_driveWheels, steeringSet, added);
            AddWheelSet(_steeringWheels, steeringSet, added);
        }

        /// <summary>
        /// Iterates wheelSet and registers each unique, non-null wheel as a WheelVisual, marking
        /// it as steerable if it is present in steeringSet.
        /// </summary>
        private void AddWheelSet(
            Transform[] wheelSet,
            HashSet<Transform> steeringSet,
            HashSet<Transform> added)
        {
            if (wheelSet == null)
                return;

            for (int i = 0; i < wheelSet.Length; i++)
            {
                Transform wheel = wheelSet[i];

                if (wheel == null || added.Contains(wheel))
                    continue;

                added.Add(wheel);
                _wheels.Add(CreateWheelVisual(wheel, steeringSet.Contains(wheel)));
            }
        }

        /// <summary>
        /// Inserts a "_MotionPivot" parent between the wheel and its original parent, preserving
        /// world position and scale, so the pivot can be yawed for steering without affecting spin.
        /// </summary>
        private WheelVisual CreateWheelVisual(Transform wheel, bool canSteer)
        {
            Transform originalParent = wheel.parent;
            Vector3 localPosition = wheel.localPosition;
            Quaternion localRotation = wheel.localRotation;
            Vector3 localScale = wheel.localScale;

            GameObject pivotObject = new GameObject(wheel.name + "_MotionPivot");
            pivotObject.layer = wheel.gameObject.layer;
            pivotObject.transform.SetParent(originalParent, false);
            pivotObject.transform.localPosition = localPosition;
            pivotObject.transform.localRotation = localRotation;
            pivotObject.transform.localScale = Vector3.one;

            wheel.SetParent(pivotObject.transform, false);
            wheel.localPosition = Vector3.zero;
            wheel.localRotation = Quaternion.identity;
            wheel.localScale = localScale;

            return new WheelVisual(pivotObject.transform, wheel, localRotation, canSteer);
        }

        /// <summary>
        /// Measures per-frame velocity from position delta; falls back to agent.velocity when
        /// displacement is below _moveThreshold (avoids jitter when nearly stationary).
        /// </summary>
        private Vector3 GetVelocity(float deltaTime)
        {
            if (!_hasLastPosition)
            {
                _lastPosition = transform.position;
                _hasLastPosition = true;
                return Vector3.zero;
            }

            Vector3 velocity = (transform.position - _lastPosition) / deltaTime;
            _lastPosition = transform.position;
            velocity.y = 0f;

            if (velocity.sqrMagnitude > _moveThreshold * _moveThreshold)
                return velocity;

            if (_agent != null && _agent.enabled)
            {
                Vector3 agentVelocity = _agent.velocity;
                agentVelocity.y = 0f;
                return agentVelocity;
            }

            return velocity;
        }

        /// <summary>
        /// Returns the desired steer angle, preferring NavMeshVehicleMotor.CurrentSteerAngle when
        /// available, otherwise computing it from the agent's steeringTarget or fallback velocity.
        /// </summary>
        private float GetTargetSteerAngle(Vector3 fallbackVelocity)
        {
            if (_vehicleMotor != null)
                return _vehicleMotor.CurrentSteerAngle;

            if (_agent != null && _agent.enabled)
            {
                Vector3 pathDirection = Vector3.zero;

                if (_agent.hasPath && !_agent.pathPending)
                {
                    pathDirection = _agent.steeringTarget - transform.position;
                    pathDirection.y = 0f;
                }

                if (pathDirection.sqrMagnitude > 0.01f)
                    return GetSteerAngleFromDirection(pathDirection);
            }

            return GetSteerAngleFromDirection(fallbackVelocity);
        }

        /// <summary>
        /// Converts a world-space steering direction to a local steer angle in degrees, clamped
        /// to _maxSteerAngle and scaled by _steerDirection.
        /// </summary>
        private float GetSteerAngleFromDirection(Vector3 steeringVelocity)
        {
            if (steeringVelocity.sqrMagnitude > _moveThreshold * _moveThreshold)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(steeringVelocity);
                float forwardMagnitude = Mathf.Max(Mathf.Abs(localVelocity.z), 0.05f);
                float targetSteerAngle = Mathf.Atan2(localVelocity.x, forwardMagnitude) * Mathf.Rad2Deg;
                targetSteerAngle = Mathf.Clamp(targetSteerAngle, -_maxSteerAngle, _maxSteerAngle);
                return targetSteerAngle * _steerDirection;
            }

            return 0f;
        }

        /// <summary>
        /// Smoothly moves _currentSteerAngle toward targetSteerAngle and applies the result as a
        /// Y rotation on the pivot of each steerable wheel.
        /// </summary>
        private void UpdateSteering(float targetSteerAngle, float deltaTime)
        {
            float steerBlend = 1f - Mathf.Exp(-_steerResponsiveness * deltaTime);
            float blendedTarget = Mathf.LerpAngle(_currentSteerAngle, targetSteerAngle, steerBlend);
            _currentSteerAngle = Mathf.MoveTowardsAngle(
                _currentSteerAngle,
                blendedTarget,
                Mathf.Max(1f, _maxSteerSpeed) * deltaTime);

            for (int i = 0; i < _wheels.Count; i++)
            {
                WheelVisual wheel = _wheels[i];

                if (!wheel.CanSteer)
                    continue;

                wheel.Pivot.localRotation = wheel.BaseRotation * Quaternion.Euler(0f, _currentSteerAngle, 0f);
            }
        }

        /// <summary>
        /// Accumulates a spin angle from forward speed divided by wheel circumference, then applies
        /// it as an X rotation on every wheel mesh transform.
        /// </summary>
        private void UpdateSpin(Vector3 velocity, float deltaTime)
        {
            float localForwardSpeed = transform.InverseTransformDirection(velocity).z;

            if (Mathf.Abs(localForwardSpeed) < _moveThreshold)
                return;

            float circumference = Mathf.Max(0.05f, Mathf.PI * 2f * _wheelRadius);
            _spinAngle += localForwardSpeed / circumference * 360f * _spinMultiplier * _spinDirection * deltaTime;

            for (int i = 0; i < _wheels.Count; i++)
            {
                WheelVisual wheel = _wheels[i];
                wheel.Wheel.localRotation = Quaternion.Euler(_spinAngle, 0f, 0f);
            }
        }

        /// <summary>
        /// Holds references to the pivot (steer parent) and wheel (spin child) transforms for one
        /// wheel, along with its base rotation and steerability flag.
        /// </summary>
        private sealed class WheelVisual
        {
            public WheelVisual(Transform pivot, Transform wheel, Quaternion baseRotation, bool canSteer)
            {
                Pivot = pivot;
                Wheel = wheel;
                BaseRotation = baseRotation;
                CanSteer = canSteer;
            }

            public Transform Pivot { get; }
            public Transform Wheel { get; }
            public Quaternion BaseRotation { get; }
            public bool CanSteer { get; }
        }
    }
}
