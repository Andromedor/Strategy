using UnityEngine;

namespace UnitController
{
    public class TrackedVehicleMotor : NavMeshVehicleMotor
    {
        [SerializeField] private float _pivotTrackSpeed = 2.4f;
        [SerializeField] private float _minimumPivotTrackSpeed = 1.1f;
        [SerializeField] private float _curveDifferentialMultiplier = 0.7f;
        [SerializeField] private float _minimumCurveDifferentialSpeed = 0.35f;
        [SerializeField] private float _fullDifferentialAngle = 75f;
        [SerializeField] private float _trackResponse = 12f;

        private float _leftTrackSpeed;
        private float _rightTrackSpeed;
        private float _turnAmount;
        private bool _isPivoting;

        public float LeftTrackSpeed => _leftTrackSpeed;
        public float RightTrackSpeed => _rightTrackSpeed;
        public float TurnAmount => _turnAmount;
        public bool IsPivoting => _isPivoting;

        protected override void OnEnable()
        {
            base.OnEnable();
            _leftTrackSpeed = 0f;
            _rightTrackSpeed = 0f;
            _turnAmount = 0f;
            _isPivoting = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _leftTrackSpeed = 0f;
            _rightTrackSpeed = 0f;
            _turnAmount = 0f;
            _isPivoting = false;
        }

        protected override void OnMotorUpdated(
            Vector3 headingDirection,
            bool hasHeading,
            float signedHeadingAngle,
            float deltaTime)
        {
            float targetLeft = 0f;
            float targetRight = 0f;
            float targetTurn = 0f;
            bool targetPivoting = false;

            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                float forwardSpeed = Mathf.Max(0f, CurrentSpeed);
                bool isMovingToPath =
                    hasHeading &&
                    (Agent.hasPath || Agent.pathPending) &&
                    Agent.remainingDistance > Agent.stoppingDistance + 0.05f;

                if (isMovingToPath)
                {
                    float fullTurnAngle = Mathf.Max(1f, _fullDifferentialAngle);
                    targetTurn = Mathf.Clamp(signedHeadingAngle / fullTurnAngle, -1f, 1f);

                    if (WaitingForAlignment)
                    {
                        float turnSign = Mathf.Sign(signedHeadingAngle);
                        if (Mathf.Approximately(turnSign, 0f))
                            turnSign = 1f;

                        float pivotMagnitude = Mathf.Lerp(
                            Mathf.Max(0f, _minimumPivotTrackSpeed),
                            Mathf.Max(_minimumPivotTrackSpeed, _pivotTrackSpeed),
                            Mathf.InverseLerp(AlignmentResumeAngle, 180f, Mathf.Abs(signedHeadingAngle)));

                        targetLeft = turnSign * pivotMagnitude;
                        targetRight = -turnSign * pivotMagnitude;
                        targetPivoting = true;
                    }
                    else
                    {
                        float curveMagnitude = Mathf.Max(
                            _minimumCurveDifferentialSpeed,
                            Mathf.Max(forwardSpeed, CruiseSpeed * 0.25f) * _curveDifferentialMultiplier);
                        float differential = targetTurn * curveMagnitude;

                        targetLeft = forwardSpeed + differential;
                        targetRight = forwardSpeed - differential;
                    }
                }
                else if (Mathf.Abs(CurrentSpeed) > 0.03f)
                {
                    targetLeft = CurrentSpeed;
                    targetRight = CurrentSpeed;
                }
            }

            float blend = 1f - Mathf.Exp(-Mathf.Max(1f, _trackResponse) * deltaTime);
            _leftTrackSpeed = SnapSmall(Mathf.Lerp(_leftTrackSpeed, targetLeft, blend));
            _rightTrackSpeed = SnapSmall(Mathf.Lerp(_rightTrackSpeed, targetRight, blend));
            _turnAmount = Mathf.Lerp(_turnAmount, targetTurn, blend);
            _isPivoting = targetPivoting;
        }

        private static float SnapSmall(float value)
        {
            return Mathf.Abs(value) < 0.01f ? 0f : value;
        }
    }
}
