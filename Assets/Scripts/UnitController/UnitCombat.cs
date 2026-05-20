using System.Collections;
using Building_and_creat_Uniit;
using UnitController;
using UnityEngine;
using UnityEngine.AI;

public class UnitCombat : MonoBehaviour, IDamageable
{
    [Header("Data")]
    [SerializeField] private UnitData _unitData;
    
    [Header("References")]
    [SerializeField] private Transform _pointPosition;
    
    private NavMeshAgent _agent;
    private Coroutine _attackCoroutine; //Поточна корутина атаки.
    private Transform _manualAttackTarget; // Ціль, яку гравець задав вручну правим кліком.
    private UnitHealth _health;
    private TeamComponent _teamComponent;
    private Transform _currentAttackTarget;
    private LayerMask _targetMask;
    public TeamType Team => _teamComponent.Team;

    
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _teamComponent = GetComponent<TeamComponent>();
        _health = new UnitHealth(_unitData.MaxHealth);
        SetupTargetMask();
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
    
    public void TakeDamage(float damage)
    {
        _health.TakeDamage(damage);

        if (_health.IsDead)
            Die();
    }

    private void Die()
    {
        EventManager.OnUnitDeselected?.Invoke(gameObject);
        Destroy(gameObject);
    }

    private void CheckEnemies()
    {
        Transform target = _manualAttackTarget;

        // Якщо manual target помер або зник —
        // очищаємо його та шукаємо нового.
        if (!IsTargetValid(target))
        {
            _manualAttackTarget = null;
            target = FindAutoTarget();
        }

        // Якщо цілі нема — зупиняємо атаку.
        if (!IsTargetValid(target))
        {
            StopAttack();
            return;
        }

        // Якщо ворог далеко —
        // під'їжджаємо.
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > _unitData.AttackRange)
        {
            MoveToAttackRange(target);
            StopAttack();
            return;
        }
        
        // Якщо ворог у range —
        // зупиняємо NavMesh рух.
        _agent.ResetPath();
        
        EventManager.OnUnitAttackTargetChanged?.Invoke(gameObject, target);
        
        if (_attackCoroutine == null || _currentAttackTarget != target)
        {
            StopAttack();

            _currentAttackTarget = target;
            _attackCoroutine = StartCoroutine(Attack(target));
        }
    }
    
    private Transform FindAutoTarget()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            _unitData.AttackRange,_targetMask
        );

        Transform closestTarget = null;
        float closestDistanceSqr = float.MaxValue;

        foreach (Collider hit in hits)
        {
            ITeam targetTeam = hit.GetComponentInParent<ITeam>();

            if (targetTeam == null)
                continue;

            if (targetTeam.Team == Team)
                continue;

            Transform target = hit.transform;

            if (!IsTargetValid(target))
                continue;

            float distanceSqr = (target.position - transform.position).sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestTarget = target;
            }
        }

        return closestTarget;
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
        while (IsTargetValid(target))
        {
            GameObject bullet = BulletPool.Instance.GetBullet();

            bullet.transform.position = _pointPosition.position;
            bullet.transform.rotation = Quaternion.identity;

            BulletController bulletController = bullet.GetComponent<BulletController>();
            bulletController.Initialize(_unitData.Damage, _unitData.Speed, target, gameObject);
            
            yield return new WaitForSeconds(_unitData.AttackDelay);
        }
        if (_manualAttackTarget == target)
            _manualAttackTarget = null;

        if (_currentAttackTarget == target)
            _currentAttackTarget = null;

        _attackCoroutine = null;
    }
    
    private void StopAttack()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        _currentAttackTarget = null;
    }
    
    private void SetupTargetMask()
    {
        if (Team == TeamType.Player)
        {
            _targetMask =
                LayerMask.GetMask("EnemyUnit");
        }
        else
        {
            _targetMask =
                LayerMask.GetMask("PlayerUnit");
        }
    }
    
    private bool IsTargetValid(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }
}
