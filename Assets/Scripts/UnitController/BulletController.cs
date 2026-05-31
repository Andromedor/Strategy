using Strategy.Buildings;
using UnityEngine;

namespace Strategy.Units
{
    /// <summary>
    /// Керує однією пулею з пулу: переміщує її до цілі щокадру та завдає шкоду при контакті з тригером,
    /// потім повертає себе до BulletPool. Очищається та повторно ініціалізується BulletPool при кожному повторному використанні.
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
        /// Встановлює шкоду, швидкість, ціль самонаведення та власника перед активацією кулі з пулу.
        /// </summary>
        public void Initialize(float damage, float speed, Transform target, GameObject owner)
        {
            _damage = damage;
            _speed = speed;
            _target = target;
            _owner = owner;
        }

        /// <summary>
        /// Переміщує кулю до _target за допомогою MoveTowards; повертає до пулу, якщо ціль стає
        /// недійсною (null або неактивна).
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
        /// При контакті з тригером: пропускає власні колайдери та союзні цілі, завдає шкоду
        /// першому IDamageable при влученні, потім повертає кулю до BulletPool.
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
        /// Повертає цю кулю до BulletPool.Instance, або деактивує GameObject, якщо пул
        /// недоступний.
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
