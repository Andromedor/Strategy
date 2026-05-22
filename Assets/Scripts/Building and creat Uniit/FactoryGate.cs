using System.Collections;
using UnityEngine;

namespace Building_and_creat_Uniit
{
    public class FactoryGate: MonoBehaviour
    {
        [Header("Gate")]
        [SerializeField] private Transform _gatePivot;
        // Об'єкт воріт, який буде обертатися.

        [SerializeField] private float _closedXAngle = 0f;
        // Кут закритих воріт по X.

        [SerializeField] private float _openXAngle = -90f;
        // Кут відкритих воріт по X.

        [SerializeField] private float _rotationSpeed = 120f;
        // Швидкість відкриття/закриття у градусах за секунду.
        
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
            while (Mathf.Abs(Mathf.DeltaAngle(_gatePivot.localEulerAngles.x, targetXAngle)) > 0.5f)
            {
                float newX = Mathf.MoveTowardsAngle(
                    _gatePivot.localEulerAngles.x,
                    targetXAngle,
                    _rotationSpeed * Time.deltaTime
                );

                _gatePivot.localRotation = Quaternion.Euler(
                    newX,
                    _gatePivot.localEulerAngles.y,
                    _gatePivot.localEulerAngles.z
                );

                yield return null;
            }
        }
    }
}