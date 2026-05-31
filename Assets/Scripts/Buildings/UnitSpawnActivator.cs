using System.Collections;
using Strategy.Units;
using UnityEngine;
using UnityEngine.AI;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Controls the transition of a newly spawned unit from its disabled factory state to fully active gameplay.
    /// Drives the unit manually to the exit point, then snaps it onto the NavMesh and enables all gameplay components.
    /// </summary>
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

        /// <summary>
        /// Toggles the unit between its disabled spawning state (colliders and gameplay off, agent stopped)
        /// and the fully active state (placed on NavMesh, gameplay enabled).
        /// </summary>
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

        /// <summary>Slides the unit toward exitPoint at _exitMoveSpeed, facing the direction of travel, then calls SetSpawningState(false).</summary>
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

        /// <summary>
        /// Samples the NavMesh near the unit's current position (with fallback radius) and warps the NavMeshAgent there.
        /// Returns false if no reachable NavMesh point is found.
        /// </summary>
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

        /// <summary>Wraps NavMesh.SamplePosition using the agent's areaMask and a clamped minimum radius.</summary>
        private bool TrySampleNavMesh(Vector3 position, float radius, out NavMeshHit hit)
        {
            return NavMesh.SamplePosition(
                position,
                out hit,
                Mathf.Max(0.1f, radius),
                _agent.areaMask);
        }

        /// <summary>Enables or disables the UnitCombat and UnitSelectionState components together.</summary>
        private void SetGameplayEnabled(bool enabled)
        {
            if (_combat != null)
                _combat.enabled = enabled;

            if (_selectionState != null)
                _selectionState.enabled = enabled;
        }

        /// <summary>Enables or disables all cached Colliders on the unit, used to prevent physics interactions while inside the factory.</summary>
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
