using Strategy.Buildings;
using UnityEngine;

namespace Strategy.Units
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

        private void OnDisable()
        {
            _target = null;
            _owner = null;
        }

        public void Initialize(float damage, float speed, Transform target, GameObject owner)
        {
            _damage = damage;
            _speed = speed;
            _target = target;
            _owner = owner;
        }

        private void FlyBullet()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                ReturnToPool();
                return;
            }

            float step = Time.deltaTime * _speed;
            transform.position = Vector3.MoveTowards(transform.position, _target.position, step);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner == null)
            {
                ReturnToPool();
                return;
            }

            if (other.transform.IsChildOf(_owner.transform))
                return;

            ITeam ownerTeam = _owner.GetComponentInParent<ITeam>();
            ITeam targetTeam = other.GetComponentInParent<ITeam>();

            if (ownerTeam != null && targetTeam != null && ownerTeam.Team == targetTeam.Team)
                return;

            IDamageable damageable = other.GetComponentInParent<IDamageable>();

            if (damageable == null)
                return;

            damageable.TakeDamage(_damage);
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (BulletPool.Instance != null)
                BulletPool.Instance.ReturnBullet(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
