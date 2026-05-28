using System.Collections;
using Building_and_creat_Uniit;
using UnitController;
using UnityEngine;
using UnityEngine.AI;

public class UnitCombat : MonoBehaviour, IDamageable
{
    [Header("Data")]
    [SerializeField] protected UnitData _unitData;

    [Header("References")]
    [SerializeField] protected Transform _pointPosition;
    [SerializeField] protected TankCannonEffects _shotEffects;

    [Header("Aiming")]
    [SerializeField] protected Transform _turret;
    [SerializeField] protected Transform _gun;

    protected NavMeshAgent _agent;
    protected TeamComponent _teamComponent;

    private Coroutine _attackCoroutine;
    private Transform _manualAttackTarget;
    private Transform _currentAttackTarget;
    private Transform _aimTarget;
    private Transform _cachedColliderTarget;
    private Collider _cachedTargetCollider;
    private UnitHealth _health;
    private LayerMask _targetMask;
    private bool _hasPlayerMoveCommand;
    private Vector3 _playerMoveDestination;
    protected float _lastTargetTime;

    public TeamType Team => _teamComponent != null ? _teamComponent.Team : TeamType.Player;

    protected virtual void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _teamComponent = GetComponent<TeamComponent>();

        if (_unitData != null)
            _health = new UnitHealth(_unitData.MaxHealth);

        if (_shotEffects == null)
            _shotEffects = GetComponent<TankCannonEffects>();

        if (_shotEffects != null)
            _shotEffects.Configure(_gun, _pointPosition);

        SetupTargetMask();
    }

    protected virtual void Update()
    {
        if (IsTargetValid(_aimTarget))
        {
            AimAtTarget(_aimTarget);
        }
        else
        {
            HandleIdleTurret();
        }
    }

    protected virtual void OnEnable()
    {
        EventManager.OnUnitMoveCommand += OnMoveCommand;
    }

    protected virtual void OnDisable()
    {
        EventManager.OnUnitMoveCommand -= OnMoveCommand;
        StopAttack();
    }

    protected virtual void Start()
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
        if (_health == null)
            return;

        _health.TakeDamage(damage);

        if (_health.IsDead)
            Die();
    }

    private void Die()
    {
        EventManager.OnUnitDeselected?.Invoke(gameObject);
        Destroy(gameObject);
    }

    private void OnMoveCommand(GameObject unit, Vector3 destination)
    {
        if (unit != gameObject)
            return;

        _hasPlayerMoveCommand = true;
        _playerMoveDestination = destination;
    }

    private void CheckEnemies()
    {
        if (_unitData == null)
            return;

        if (_hasPlayerMoveCommand && HasReachedPlayerMoveDestination())
            _hasPlayerMoveCommand = false;

        Transform target = _manualAttackTarget;

        if (!IsTargetValid(target))
        {
            _manualAttackTarget = null;
            target = FindAutoTarget();
        }

        if (!IsTargetValid(target))
        {
            StopAttack();
            _aimTarget = null;
            return;
        }

        _aimTarget = target;
        _lastTargetTime = Time.time;

        float distance = Vector3.Distance(transform.position, target.position);

        if (_hasPlayerMoveCommand)
        {
            if (distance <= _unitData.AttackRange && AimAtTarget(target))
                StartAttackIfNeeded(target);
            else
                StopAttack();

            return;
        }

        if (distance > _unitData.AttackRange)
        {
            MoveToAttackRange(target);
            StopAttack();
            return;
        }

        if (_agent != null && _agent.enabled)
            _agent.ResetPath();

        if (!AimAtTarget(target))
        {
            StopAttack();
            return;
        }

        StartAttackIfNeeded(target);
    }

    private void StartAttackIfNeeded(Transform target)
    {
        EventManager.OnUnitAttackTargetChanged?.Invoke(gameObject, target);

        if (_attackCoroutine != null && _currentAttackTarget == target)
            return;

        StopAttack();

        _currentAttackTarget = target;
        _attackCoroutine = StartCoroutine(Attack(target));
    }

    private bool HasReachedPlayerMoveDestination()
    {
        if (_agent == null || !_agent.enabled)
            return true;

        if (!_agent.hasPath && !_agent.pathPending)
            return false;

        return !_agent.pathPending &&
               _agent.remainingDistance <= _agent.stoppingDistance + 0.2f;
    }

    private Transform FindAutoTarget()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            _unitData.AttackRange,
            _targetMask);

        Transform closestTarget = null;
        float closestDistanceSqr = float.MaxValue;

        foreach (Collider hit in hits)
        {
            ITeam targetTeam = hit.GetComponentInParent<ITeam>();

            if (targetTeam == null || targetTeam.Team == Team)
                continue;

            Transform target = hit.transform;

            if (!IsTargetValid(target))
                continue;

            float distanceSqr = (target.position - transform.position).sqrMagnitude;

            if (distanceSqr >= closestDistanceSqr)
                continue;

            closestDistanceSqr = distanceSqr;
            closestTarget = target;
        }

        return closestTarget;
    }

    private void MoveToAttackRange(Transform target)
    {
        if (_agent == null || !_agent.enabled)
            return;

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
            yield return FireAtTarget(target);
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
        _targetMask = Team == TeamType.Player
            ? LayerMask.GetMask("EnemyUnit")
            : LayerMask.GetMask("PlayerUnit");
    }

    protected virtual IEnumerator FireAtTarget(Transform target)
    {
        if (_unitData == null || _pointPosition == null || target == null)
            yield break;

        GameObject bullet = BulletPool.Instance.GetBullet();

        bullet.transform.position = _pointPosition.position;
        bullet.transform.rotation = _pointPosition.rotation;

        BulletController bulletController = bullet.GetComponent<BulletController>();

        if (bulletController == null)
        {
            BulletPool.Instance.ReturnBullet(bullet);
            yield break;
        }

        bulletController.Initialize(_unitData.Damage, _unitData.Speed, target, gameObject);
        _shotEffects?.PlayShotEffect();
    }

    protected virtual bool AimAtTarget(Transform target)
    {
        if (target == null || _turret == null || _gun == null || _unitData == null)
            return false;

        bool turretReady = RotateTurretToTarget(target);
        bool gunReady = RotateGunToTarget(target);

        return turretReady && gunReady;
    }

    protected virtual bool RotateTurretToTarget(Transform target)
    {
        Vector3 worldDirection = target.position - _turret.position;
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
        localDirection.y = 0f;

        if (localDirection.sqrMagnitude < 0.01f)
            return true;

        float targetYaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float currentYaw = _turret.localEulerAngles.y;
        float newYaw = Mathf.MoveTowardsAngle(
            currentYaw,
            targetYaw,
            _unitData.TurretRotationSpeed * Time.deltaTime);

        _turret.localRotation = Quaternion.Euler(0f, newYaw, 0f);

        return Mathf.Abs(Mathf.DeltaAngle(newYaw, targetYaw)) <= _unitData.AimAngleTolerance;
    }

    protected virtual bool RotateGunToTarget(Transform target)
    {
        Vector3 worldDirection = target.position - _gun.position;
        Vector3 localDirection = _turret.InverseTransformDirection(worldDirection);
        float horizontalDistance = new Vector2(localDirection.x, localDirection.z).magnitude;
        float targetPitch = -Mathf.Atan2(localDirection.y, horizontalDistance) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, _unitData.MinGunPitch, _unitData.MaxGunPitch);

        return MoveGunPitch(targetPitch);
    }

    protected bool MoveGunPitch(float targetPitch)
    {
        float currentPitch = _gun.localEulerAngles.x;
        float newPitch = Mathf.MoveTowardsAngle(
            currentPitch,
            targetPitch,
            _unitData.GunPitchSpeed * Time.deltaTime);

        _gun.localRotation = Quaternion.Euler(newPitch, 0f, 0f);

        return Mathf.Abs(Mathf.DeltaAngle(newPitch, targetPitch)) <= _unitData.AimAngleTolerance;
    }

    protected virtual void HandleIdleTurret()
    {
        if (_unitData == null || _turret == null || _gun == null)
            return;

        if (Time.time < _lastTargetTime + _unitData.ReturnTurretDelay)
            return;

        float currentYaw = _turret.localEulerAngles.y;
        float newYaw = Mathf.MoveTowardsAngle(
            currentYaw,
            0f,
            _unitData.IdleTurretRotationSpeed * Time.deltaTime);

        _turret.localRotation = Quaternion.Euler(0f, newYaw, 0f);

        MoveGunPitch(0f);
    }

    protected bool IsTargetValid(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }

    protected Vector3 GetTargetPoint(Transform target)
    {
        if (target != _cachedColliderTarget)
        {
            _cachedColliderTarget = target;
            _cachedTargetCollider = target.GetComponentInParent<Collider>();
        }

        if (_cachedTargetCollider != null)
            return _cachedTargetCollider.bounds.center;

        return target.position;
    }

    protected float GetDistanceRatio(Vector3 targetPoint)
    {
        Vector2 from = new Vector2(transform.position.x, transform.position.z);
        Vector2 to = new Vector2(targetPoint.x, targetPoint.z);
        float distance = Vector2.Distance(from, to);

        if (_unitData == null || _unitData.AttackRange <= 0f)
            return 1f;

        return Mathf.Clamp01(distance / _unitData.AttackRange);
    }

    protected bool IsMoving(float speedThreshold)
    {
        if (_agent == null || !_agent.enabled)
            return false;

        return _agent.velocity.magnitude >= speedThreshold;
    }

    protected Vector3 SnapToGround(Vector3 point)
    {
        Vector3 origin = point + Vector3.up * 30f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return point;
    }
}
