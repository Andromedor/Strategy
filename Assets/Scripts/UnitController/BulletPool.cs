using System.Collections.Generic;
using Strategy.Core;
using UnityEngine;

namespace Strategy.Units
{
    /// <summary>
    /// Синглтон-пул об'єктів для куль. Попередньо створює _poolSize куль при Awake
    /// та переробляє їх через TryGetBullet / ReturnBullet, щоб уникнути виділення пам'яті під час бою.
    /// </summary>
    public class BulletPool : MonoBehaviour
    {
        public static BulletPool Instance { get; private set; }

        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private int _poolSize = 200;

        private readonly Queue<GameObject> _bulletPool = new();
        private Transform _bulletContainer;

        /// <summary>
        /// Скидає статичне посилання Instance між перезавантаженнями домену / сесіями режиму гри.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _bulletContainer = RuntimeObjectContainer.Get("Bullets");

            for (int i = _bulletPool.Count; i < _poolSize; i++)
            {
                GameObject bullet = CreateBullet();
                if (bullet != null)
                    _bulletPool.Enqueue(bullet);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Вилучає кулю з пулу (або створює нову, якщо пул порожній), активує її
        /// та повертає true. Повертає false лише якщо префаб є null.
        /// </summary>
        public bool TryGetBullet(out GameObject bullet)
        {
            bullet = null;

            while (_bulletPool.Count > 0 && bullet == null)
                bullet = _bulletPool.Dequeue();

            if (bullet == null)
                bullet = CreateBullet();

            if (bullet == null)
                return false;

            bullet.SetActive(true);
            return true;
        }

        /// <summary>
        /// Зручна обгортка навколо TryGetBullet; повертає null замість false, коли
        /// куля недоступна.
        /// </summary>
        public GameObject GetBullet()
        {
            return TryGetBullet(out GameObject bullet) ? bullet : null;
        }

        /// <summary>
        /// Деактивує кулю, повертає її до контейнера пулу та додає до черги для повторного використання.
        /// </summary>
        public void ReturnBullet(GameObject bullet)
        {
            if (bullet == null)
                return;

            bullet.SetActive(false);

            if (_bulletContainer != null)
                bullet.transform.SetParent(_bulletContainer, false);

            _bulletPool.Enqueue(bullet);
        }

        /// <summary>
        /// Створює нову кулю з префабу під контейнером пулу та одразу
        /// деактивує її. Повертає null, якщо bulletPrefab не призначений.
        /// </summary>
        private GameObject CreateBullet()
        {
            if (bulletPrefab == null)
                return null;

            GameObject bullet = Instantiate(bulletPrefab, _bulletContainer);
            bullet.SetActive(false);
            return bullet;
        }
    }
}
