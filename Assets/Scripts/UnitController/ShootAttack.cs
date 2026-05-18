using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ShootAttack : MonoBehaviour
{
    [SerializeField] private float _rangeAttack = 40f;
    [SerializeField] private float _attackDelay = 2f;
    [SerializeField] private Transform _pointPosition;
    [SerializeField] private float _health = 100f;
    
    private NavMeshAgent _agent;
    private Coroutine _attackCoroutine;
    
    public float Health
    {
        get => _health;
        set => _health = value;
    }
    
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }
    
    private void Start()
    {
        InvokeRepeating(nameof(CheckEnemies), 0f, 0.25f);
    }

    private void CheckEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _rangeAttack);
        
        Collider target = null;
        
        foreach (Collider hit in hits)
        {
            bool isEnemy = (CompareTag("Player") && hit.CompareTag("Enemy")) ||
                           (CompareTag("Enemy") && hit.CompareTag("Player"));

            if (isEnemy)
            {
                target = hit;
                break;
            }
        }

        if (target != null)
        {
            EventManager.OnUnitAttackTargetChanged?.Invoke(gameObject, target.transform);
            
            if (target.CompareTag("Enemy"))
            {
                _agent.SetDestination(target.transform.position);
            }
            
            if (_attackCoroutine == null)
            {
                _attackCoroutine = StartCoroutine(Attack(target.transform));
            }
        }
        else
        {
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }
        }
    }

    private IEnumerator Attack(Transform target)
    {
        while (target != null)
        {
            GameObject bullet = BulletPool.Instance.GetBullet();

            bullet.transform.position = _pointPosition.position;
            bullet.transform.rotation = Quaternion.identity;

            bullet.GetComponent<BulletController>()
                .TargetPosition = target.position;

            yield return new WaitForSeconds(_attackDelay);
        }

        _attackCoroutine = null;
    }
}
