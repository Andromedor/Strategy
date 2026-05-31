using System.Collections;
using UnityEngine;

namespace Strategy.Buildings
{
    /// <summary>
    /// Animates a factory gate pivot between open and closed X-rotation angles.
    /// Open/Close coroutines are awaited by BuildingProduction during unit spawning.
    /// </summary>
    public class FactoryGate : MonoBehaviour
    {
        [Header("Gate")]
        [SerializeField] private Transform _gatePivot;
        [SerializeField] private float _closedXAngle = 0f;
        [SerializeField] private float _openXAngle = -90f;
        [SerializeField] private float _rotationSpeed = 120f;

        /// <summary>Coroutine that rotates the gate pivot to the open X angle.</summary>
        public IEnumerator Open()
        {
            yield return RotateGate(_openXAngle);
        }

        /// <summary>Coroutine that rotates the gate pivot back to the closed X angle.</summary>
        public IEnumerator Close()
        {
            yield return RotateGate(_closedXAngle);
        }

        /// <summary>Incrementally rotates the gate pivot toward targetXAngle at _rotationSpeed degrees/second, preserving Y and Z.</summary>
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
