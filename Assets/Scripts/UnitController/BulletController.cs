using System;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] private float _speed;
    [SerializeField] private float _damage;
    
    [NonSerialized] public Vector3 TargetPosition;
    
    private void Update()
    {
        FlyBullet();
    }

    private void FlyBullet()
    {
       float step = Time.deltaTime * _speed;
       transform.position = Vector3.MoveTowards(transform.position, TargetPosition, step);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player" ) || other.CompareTag("Enemy"))
        {
       
            BulletPool.Instance.ReturnBullet(gameObject);
            ShootAttack shootAttack = other.GetComponent<ShootAttack>();
            shootAttack.Health -= _damage;
            
            if (shootAttack.Health <= 0)
            {
                Destroy(other.gameObject);
            }
        }
    }
}