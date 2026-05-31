using System.Collections;
using UnityEngine;

namespace Strategy.Buildings
{
    public class FactoryGate : MonoBehaviour
    {
        [Header("Gate")]
        [SerializeField] private Transform _gatePivot;
        [SerializeField] private float _closedXAngle = 0f;
        [SerializeField] private float _openXAngle = -90f;
        [SerializeField] private float _rotationSpeed = 120f;

        public IEnumerator Open()
        {
            yield return RotateGate(_openXAngle);
        }

        public IEnumerator Close()
        {
            yield return RotateGate(_closedXAngle);
        }

        private IEnumerator RotateGate(float targetXAngle)
        {
            if (_gatePivot == null)
                yield break;

            while (Mathf.Abs(Mathf.DeltaAngle(_gatePivot.localEulerAngles.x, targetXAngle)) > 0.5f)
            {
                float newX = Mathf.MoveTowardsAngle(
                    _gatePivot.localEulerAngles.x,
                    targetXAngle,
                    Mathf.Max(0f, _rotationSpeed) * Time.deltaTime);

                Vector3 currentEuler = _gatePivot.localEulerAngles;
                _gatePivot.localRotation = Quaternion.Euler(newX, currentEuler.y, currentEuler.z);

                yield return null;
            }

            Vector3 euler = _gatePivot.localEulerAngles;
            _gatePivot.localRotation = Quaternion.Euler(targetXAngle, euler.y, euler.z);
        }
    }
}
