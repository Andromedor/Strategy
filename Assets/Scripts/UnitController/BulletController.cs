using Building_and_creat_Uniit;
using UnityEngine;

namespace UnitController
{
    public class BulletController : MonoBehaviour
    {
        private float Speed;
        private float Damage;
        private Vector3 TargetPosition;

        private void Update()
        {
            FlyBullet();
        }

        public void Initialize (float damage, float speed, Vector3 targetPosition)
        {
            Damage = damage;
            Speed = speed;
            TargetPosition = targetPosition;
        }

        private void FlyBullet()
        {
            float step = Time.deltaTime * Speed;
            transform.position = Vector3.MoveTowards(transform.position, TargetPosition, step);
            
            if (Vector3.Distance(transform.position, TargetPosition) < 0.1f)
            {
                BulletPool.Instance.ReturnBullet(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();

            if (damageable == null)
                return;

            damageable.TakeDamage(Damage);
            BulletPool.Instance.ReturnBullet(gameObject);
        }
    }
}