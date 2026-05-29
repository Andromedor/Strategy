using System.Collections;
using UnitController;
using UnityEngine;
using UnityEngine.AI;

namespace Building_and_creat_Uniit
{
    public class UnitSpawnActivator : MonoBehaviour
    {
        [SerializeField] private float _exitMoveSpeed = 4f;
        // Швидкість ручного виїзду з заводу.

        [SerializeField] private float _exitDistance = 0.2f;
        // Наскільки близько треба під'їхати до ExitPoint.

        [SerializeField] private float _navMeshSnapRadius = 6f;
        [SerializeField] private float _navMeshFallbackSnapRadius = 14f;

        private NavMeshAgent _agent;
        private UnitCombat _combat;
        private UnitSelectionState _selectionState;
        private NavMeshVehicleMotor _vehicleMotor;
        private Collider[] _colliders;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _combat = GetComponent<UnitCombat>();
            _selectionState = GetComponent<UnitSelectionState>();
            _vehicleMotor = GetComponent<NavMeshVehicleMotor>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        public void SetSpawningState(bool isSpawning)
        {
            if (isSpawning)
            {
                SetGameplayEnabled(false);

                if (_vehicleMotor != null)
                    _vehicleMotor.enabled = false;

                if (_agent != null)
                {
                    if (_agent.enabled && _agent.isOnNavMesh)
                        _agent.ResetPath();

                    _agent.enabled = false;
                }

                SetCollidersEnabled(false);
            }
            else
            {
                if (!EnableNavigationAtCurrentPosition())
                {
                    Debug.LogError(
                        $"{name} could not be activated after factory exit because no reachable NavMesh was found near {transform.position}.",
                        this);
                    return;
                }

                if (_vehicleMotor != null)
                    _vehicleMotor.enabled = true;

                SetCollidersEnabled(true);
                SetGameplayEnabled(true);
            }
        }

        public IEnumerator MoveOutOfFactory(Vector3 exitPoint)
        {
            while (Vector3.Distance(transform.position, exitPoint) > _exitDistance)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    exitPoint,
                    _exitMoveSpeed * Time.deltaTime
                );

                Vector3 direction = exitPoint - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(direction);

                yield return null;
            }

            transform.position = exitPoint;

            SetSpawningState(false);
        }

        private bool EnableNavigationAtCurrentPosition()
        {
            if (_agent == null)
                return false;

            if (!TrySampleNavMesh(transform.position, _navMeshSnapRadius, out NavMeshHit hit) &&
                !TrySampleNavMesh(transform.position, _navMeshFallbackSnapRadius, out hit))
                return false;

            Vector3 navPosition = hit.position;
            transform.position = navPosition;
            _agent.enabled = true;

            if (!_agent.Warp(navPosition) || !_agent.isOnNavMesh)
            {
                _agent.enabled = false;
                return false;
            }

            _agent.updatePosition = true;
            _agent.updateRotation = false;
            _agent.ResetPath();
            return true;
        }

        private bool TrySampleNavMesh(Vector3 position, float radius, out NavMeshHit hit)
        {
            return NavMesh.SamplePosition(
                position,
                out hit,
                Mathf.Max(0.1f, radius),
                _agent.areaMask);
        }

        private void SetGameplayEnabled(bool enabled)
        {
            if (_combat != null)
                _combat.enabled = enabled;

            if (_selectionState != null)
                _selectionState.enabled = enabled;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null)
                return;

            foreach (Collider unitCollider in _colliders)
            {
                if (unitCollider != null)
                    unitCollider.enabled = enabled;
            }
        }
    }
}
