using Strategy.Buildings;
using UnityEngine;

namespace Strategy.Units
{
    /// <summary>
    /// Controls a single pooled bullet: moves it toward its target each frame and deals damage on
    /// trigger contact, then returns itself to BulletPool. Cleared and re-initialised by BulletPool
    /// for each reuse.
    /// </summary>
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

        /// <summary>
        /// Sets damage, speed, homing target, and owner before the bullet is activated from the pool.
        /// </summary>
        public void Initialize(float damage, float speed, Transform target, GameObject owner)
        {
            _damage = damage;
            _speed = speed;
            _target = target;
            _owner = owner;
        }

        /// <summary>
        /// Moves the bullet toward _target using MoveTowards; returns to pool if the target becomes
        /// invalid (null or inactive).
        /// </summary>
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

        /// <summary>
        /// On trigger contact: skips the owner's own colliders and friendly targets, applies damage
        /// to the first IDamageable hit, then returns the bullet to BulletPool.
        /// </summary>
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

        /// <summary>
        /// Returns this bullet to BulletPool.Instance, or deactivates the GameObject if the pool
        /// is unavailable.
        /// </summary>
        private void ReturnToPool()
        {
            if (BulletPool.Instance != null)
                BulletPool.Instance.ReturnBullet(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
