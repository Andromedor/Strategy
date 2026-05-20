using Building_and_creat_Uniit;
using UnityEngine;

namespace UnitController
{
    public class BulletController : MonoBehaviour
    {
        private float _speed;
        private float _damage;
        private Transform _target;
        private GameObject _owner;

        private void Update()
        {
            FlyBullet();
        }

        public void Initialize (float damage, float speed, Transform target, GameObject owner)
        {
            _damage = damage;
            _speed = speed;
            _target = target;
            _owner = owner;
        }

        private void FlyBullet()
        {
            if (_target == null)
            {
                BulletPool.Instance.ReturnBullet(gameObject);
                return;
            }

            float step = Time.deltaTime * _speed;
            transform.position = Vector3.MoveTowards(transform.position, _target.position, step);
            
            if (Vector3.Distance(transform.position, _target.position) < 0.1f)
            {
                BulletPool.Instance.ReturnBullet(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if(_owner == null) return;
            
            if (other.gameObject == _owner)
                return;

            if (other.CompareTag(_owner.tag))
                return;
            
            IDamageable damageable = other.GetComponentInParent<IDamageable>();

            if (damageable == null)
                return;

            damageable.TakeDamage(_damage);
            BulletPool.Instance.ReturnBullet(gameObject);
        }
    }
}

public enum TeamType
{
    Player,
    Enemy
}