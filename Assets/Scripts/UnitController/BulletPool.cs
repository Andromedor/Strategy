using System.Collections.Generic;
using Strategy.Core;
using UnityEngine;

namespace Strategy.Units
{
    /// <summary>
    /// Singleton object pool for bullet GameObjects. Pre-instantiates _poolSize bullets on Awake
    /// and recycles them via TryGetBullet / ReturnBullet to avoid runtime allocations during combat.
    /// </summary>
    public class BulletPool : MonoBehaviour
    {
        public static BulletPool Instance { get; private set; }

        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private int _poolSize = 200;

        private readonly Queue<GameObject> _bulletPool = new();
        private Transform _bulletContainer;

        /// <summary>
        /// Resets the static Instance reference between domain reloads / play mode sessions.
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
        /// Dequeues a bullet from the pool (or creates one if the pool is empty), activates it,
        /// and returns true. Returns false only if the prefab is null.
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
        /// Convenience wrapper around TryGetBullet; returns null instead of false when no bullet
        /// is available.
        /// </summary>
        public GameObject GetBullet()
        {
            return TryGetBullet(out GameObject bullet) ? bullet : null;
        }

        /// <summary>
        /// Deactivates the bullet, re-parents it under the pool container, and enqueues it for reuse.
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
        /// Instantiates a new bullet from the prefab under the pool container and immediately
        /// deactivates it. Returns null if bulletPrefab is not assigned.
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
