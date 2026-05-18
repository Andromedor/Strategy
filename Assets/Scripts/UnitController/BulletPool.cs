using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;
    
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int _poolSize = 200;
    
    private Queue<GameObject> _bulletPool = new Queue<GameObject>();

    private void Awake()
    {
        Instance = this;

        for (int i = 0; i < _poolSize; i++)
        {
            GameObject bullet = Instantiate(bulletPrefab);
            
            bullet.SetActive(false);
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
        
        return Instantiate(bulletPrefab);
    }

    public void ReturnBullet(GameObject bullet)
    {
        bullet.SetActive(false);
        _bulletPool.Enqueue(bullet);
    }
}
