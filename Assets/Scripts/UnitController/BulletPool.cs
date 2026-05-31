using System.Collections.Generic;
using Strategy.Core;
using UnityEngine;

namespace Strategy.Units
{
    public class BulletPool : MonoBehaviour
    {
        public static BulletPool Instance { get; private set; }

        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private int _poolSize = 200;

        private readonly Queue<GameObject> _bulletPool = new();
        private Transform _bulletContainer;

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

        public GameObject GetBullet()
        {
            return TryGetBullet(out GameObject bullet) ? bullet : null;
        }

        public void ReturnBullet(GameObject bullet)
        {
            if (bullet == null)
                return;

            bullet.SetActive(false);

            if (_bulletContainer != null)
                bullet.transform.SetParent(_bulletContainer, false);

            _bulletPool.Enqueue(bullet);
        }

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
