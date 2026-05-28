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
    [SerializeField] private TankCannonEffects _shotEffects;
    
    [Header("Aiming")]
    [SerializeField] private Transform _turret; // Башня танка. Вона крутиться навколо Y.
    [SerializeField] private Transform _gun; // Пушка танка. Вона піднімається/опускається по X.

    
    private NavMeshAgent _agent;
    private Coroutine _attackCoroutine; //Поточна корутина атаки.
    private Transform _manualAttackTarget; // Ціль, яку гравець задав вручну правим кліком.
    private UnitHealth _health;
    private TeamComponent _teamComponent;
    private ArtilleryWeapon _artilleryWeapon;
    private Transform _currentAttackTarget;
    private LayerMask _targetMask;
    public TeamType Team => _teamComponent.Team;
    
    private bool _hasPlayerMoveCommand;
// Чи є зараз активний наказ руху від гравця.

    private Vector3 _playerMoveDestination;
// Точка, куди гравець наказав рухатися.
    private Transform _aimTarget;
    private float _lastTargetTime;
    
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _teamComponent = GetComponent<TeamComponent>();
        _artilleryWeapon = GetComponent<ArtilleryWeapon>();
        _health = new UnitHealth(_unitData.MaxHealth);
        
        if (_shotEffects == null)
            _shotEffects = GetComponent<TankCannonEffects>();
        
        if (_shotEffects != null)
            _shotEffects.Configure(_gun, _pointPosition);

        if (_artilleryWeapon != null)
        {
            _artilleryWeapon.Configure(
                _unitData,
                _turret,
                _gun,
                _pointPosition,
                _shotEffects,
                _agent,
                _teamComponent);
        }
        
        SetupTargetMask();
    }
    
    private void Update()
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
    
    private void OnEnable()
    {
        EventManager.OnUnitMoveCommand += OnMoveCommand;
    }

    private void OnDisable()
    {
        EventManager.OnUnitMoveCommand -= OnMoveCommand;
    }
    
    private void OnMoveCommand(GameObject unit, Vector3 destination)
    {
        if (unit != gameObject)
            return;

        _hasPlayerMoveCommand = true;
        _playerMoveDestination = destination;
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
        if (_hasPlayerMoveCommand && HasReachedPlayerMoveDestination())
        {
            _hasPlayerMoveCommand = false;
        }

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
// Якщо є наказ руху від гравця.
        if (_hasPlayerMoveCommand)
        {// Якщо ворог ще далеко.
            if (distance <= _unitData.AttackRange)
            {
                bool isAimedMove = AimAtTarget(target); // Наводимо башню та пушку.
                if (isAimedMove)
                    StartAttackIfNeeded(target);
                else
                    StopAttack();
            }

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

        _agent.ResetPath();

        EventManager.OnUnitAttackTargetChanged?.Invoke(gameObject, target);
        
        bool isAimed = AimAtTarget(target);

        if (!isAimed)
        {
            StopAttack();
            return;
        }
        
        StartAttackIfNeeded(target);
    }
    
    private void StartAttackIfNeeded(Transform target)
    {
        EventManager.OnUnitAttackTargetChanged?.Invoke(gameObject, target);

        if (_attackCoroutine == null || _currentAttackTarget != target)
        {
            StopAttack();

            _currentAttackTarget = target;
            _attackCoroutine = StartCoroutine(Attack(target));
        }
    }

    private bool HasReachedPlayerMoveDestination()
    {
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
            return true;

        return false;
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
            if (_artilleryWeapon != null)
            {
                yield return _artilleryWeapon.Fire(target, gameObject);
            }
            else
            {
                GameObject bullet = BulletPool.Instance.GetBullet();

                bullet.transform.position = _pointPosition.position;
                bullet.transform.rotation = _pointPosition.rotation;

                BulletController bulletController = bullet.GetComponent<BulletController>();
                bulletController.Initialize(_unitData.Damage, _unitData.Speed, target, gameObject);
                _shotEffects?.PlayShotEffect();
            }
            
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

    /// <summary>
    /// Повертає башню по локальній Y-осі відносно корпусу танка.
    /// Це прибирає смикання, бо ми не задаємо world rotation напряму.
    /// </summary>
    private bool RotateTurretToTarget(Transform target)
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
            _unitData.TurretRotationSpeed * Time.deltaTime
        );

        _turret.localRotation = Quaternion.Euler(0f, newYaw, 0f);

        float angle = Mathf.Abs(Mathf.DeltaAngle(newYaw, targetYaw));

        return angle <= _unitData.AimAngleTolerance;
    }
    
    /// <summary>
    /// Піднімає або опускає пушку по локальній X-осі.
    /// Працює відносно башні.
    /// </summary>
    private bool RotateGunToTarget(Transform target)
    {
        Vector3 worldDirection = target.position - _gun.position;

        Vector3 localDirection = _turret.InverseTransformDirection(worldDirection);

        float horizontalDistance = new Vector2(
            localDirection.x,
            localDirection.z
        ).magnitude;

        float targetPitch = -Mathf.Atan2(localDirection.y, horizontalDistance) * Mathf.Rad2Deg;

        targetPitch = Mathf.Clamp(
            targetPitch,
            _unitData.MinGunPitch,
            _unitData.MaxGunPitch
        );

        float currentPitch = _gun.localEulerAngles.x;

        float newPitch = Mathf.MoveTowardsAngle(
            currentPitch,
            targetPitch,
            _unitData.GunPitchSpeed * Time.deltaTime
        );

        _gun.localRotation = Quaternion.Euler(newPitch, 0f, 0f);

        float angle = Mathf.Abs(Mathf.DeltaAngle(newPitch, targetPitch));

        return angle <= _unitData.AimAngleTolerance;
    }
    
    /// <summary>
    /// Повертає башню вперед,
    /// якщо давно нема target.
    /// </summary>
    private void HandleIdleTurret()
    {
        // Ще рано повертати.
        if (Time.time < _lastTargetTime + _unitData.ReturnTurretDelay)
            return;

        float currentYaw = _turret.localEulerAngles.y;

        float newYaw = Mathf.MoveTowardsAngle(
            currentYaw,
            0f,
            _unitData.IdleTurretRotationSpeed * Time.deltaTime
        );

        _turret.localRotation =
            Quaternion.Euler(0f, newYaw, 0f);

        // Пушку теж повертаємо.
        float currentPitch = _gun.localEulerAngles.x;

        float newPitch = Mathf.MoveTowardsAngle(
            currentPitch,
            0f,
            _unitData.GunPitchSpeed * Time.deltaTime
        );

        _gun.localRotation =
            Quaternion.Euler(newPitch, 0f, 0f);
    }
    
    /// <summary>
    /// Повертає башню і пушку до цілі.
    /// Повертає true тільки тоді, коли наведення майже завершене.
    /// </summary>
    private bool AimAtTarget(Transform target)
    {
        if (target == null)
            return false;

        if (_artilleryWeapon != null)
            return _artilleryWeapon.AimAtTarget(target);

        bool turretReady = RotateTurretToTarget(target);
        bool gunReady = RotateGunToTarget(target);

        return turretReady && gunReady;
    }
    
    private bool IsTargetValid(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }
}
