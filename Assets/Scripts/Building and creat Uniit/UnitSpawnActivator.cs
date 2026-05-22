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

        private NavMeshAgent _agent;
        private UnitCombat _combat;
        private UnitSelectionState _selectionState;
        private Collider[] _colliders;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _combat = GetComponent<UnitCombat>();
            _selectionState = GetComponent<UnitSelectionState>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        public void SetSpawningState(bool isSpawning)
        {
            if (_agent != null)
                _agent.enabled = !isSpawning;

            if (_combat != null)
                _combat.enabled = !isSpawning;

            if (_selectionState != null)
                _selectionState.enabled = !isSpawning;

            foreach (Collider unitCollider in _colliders)
                unitCollider.enabled = !isSpawning;
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
    }
}