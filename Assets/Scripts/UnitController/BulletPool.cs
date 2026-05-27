using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;
    
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int _poolSize = 200;
    
    private Queue<GameObject> _bulletPool = new Queue<GameObject>();
    private Transform _bulletContainer;

    private void Awake()
    {
        Instance = this;
        _bulletContainer = RuntimeObjectContainer.Get("Bullets");

        for (int i = 0; i < _poolSize; i++)
        {
            GameObject bullet = CreateBullet();
            _bulletPool.Enqueue(bullet);
        }
    }

    public GameObject GetBullet()
    {
        if (_bulletPool.Count > 0)
        {
            var bullet = _bulletPool.Dequeue();
            bullet.SetActive(true);
            return bullet;
        }

        GameObject newBullet = CreateBullet();
        newBullet.SetActive(true);
        return newBullet;
    }

    public void ReturnBullet(GameObject bullet)
    {
        bullet.SetActive(false);
        bullet.transform.SetParent(_bulletContainer, false);
        _bulletPool.Enqueue(bullet);
    }

    private GameObject CreateBullet()
    {
        GameObject bullet = Instantiate(bulletPrefab, _bulletContainer);
        bullet.SetActive(false);
        return bullet;
    }
}
