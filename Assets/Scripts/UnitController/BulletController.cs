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
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                BulletPool.Instance.ReturnBullet(gameObject);
                return;
            }

            float step = Time.deltaTime * _speed;
            transform.position = Vector3.MoveTowards(transform.position, _target.position, step);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner == null)
            {
                BulletPool.Instance.ReturnBullet(gameObject);
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
            BulletPool.Instance.ReturnBullet(gameObject);
        }
    }
}