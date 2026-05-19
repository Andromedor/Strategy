using System.Collections;
using UnitController;
using UnityEngine;
using UnityEngine.AI;

public class UnitCombat : MonoBehaviour
{
    [SerializeField] private UnitData _unitData;
    [SerializeField] private Transform _pointPosition;
    
    private NavMeshAgent _agent;
    private Coroutine _attackCoroutine;
    private Transform _manualAttackTarget;
    
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }
    
    private void Start()
    {
        InvokeRepeating(nameof(CheckEnemies), 0f, 0.25f);
    }
    
    public void SetManualAttackTarget(Transform target)
    {
        _manualAttackTarget = target;
        EventManager.OnUnitAttackTargetChanged?.Invoke(gameObject, target);
    }

    private void CheckEnemies()
    {
        Transform target = _manualAttackTarget;

        if (target == null)
            target = FindAutoTarget();

        if (target == null)
        {
            StopAttack();
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > _unitData.AttackRange)
        {
            MoveToAttackRange(target);
            StopAttack();
            return;
        }
        
        _agent.ResetPath();
        
        EventManager.OnUnitAttackTargetChanged?.Invoke(gameObject, target);
        
        if (_attackCoroutine == null)
            _attackCoroutine = StartCoroutine(Attack(target));
    }
    
    private Transform FindAutoTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _unitData.AttackRange);

        foreach (Collider hit in hits)
        {
            bool isEnemy =
                (CompareTag("Player") && hit.CompareTag("Enemy")) ||
                (CompareTag("Enemy") && hit.CompareTag("Player"));

            if (isEnemy)
                return hit.transform;
        }

        return null;
    }
    
    private void MoveToAttackRange(Transform target)
    {
        Vector3 directionFromTarget = (transform.position - target.position).normalized;

        if (directionFromTarget.sqrMagnitude < 0.01f)
            directionFromTarget = -transform.forward;

        Vector3 attackPosition =
            target.position + directionFromTarget * (_unitData.AttackRange * 0.85f);

        _agent.SetDestination(attackPosition);
    }


    private IEnumerator Attack(Transform target)
    {
        while (target != null)
        {
            GameObject bullet = BulletPool.Instance.GetBullet();

            bullet.transform.position = _pointPosition.position;
            bullet.transform.rotation = Quaternion.identity;

            BulletController bulletController = bullet.GetComponent<BulletController>();
            bulletController.Initialize(_unitData.Damage, _unitData.Speed, target.position);
            
            yield return new WaitForSeconds(_unitData.AttackDelay);
        }

        _attackCoroutine = null;
    }
    
    private void StopAttack()
    {
        if (_attackCoroutine == null)
            return;

        StopCoroutine(_attackCoroutine);
        _attackCoroutine = null;
    }
}
