using System;
using UnitController;
using UnityEngine;

namespace DefaultNamespace
{
    public class ResourceManager: MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public static event Action<int> OnResourceChanged;

        [SerializeField] private int _startResource = 500;
        [SerializeField] private int _startEnemyResource = 500;

        private int _resource;
        private int _enemyResource;

        public int Resource => _resource;
        public int EnemyResource => _enemyResource;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _resource = _startResource;
            _enemyResource = _startEnemyResource;

            OnResourceChanged?.Invoke(_resource);
        }

        public bool Spend(int amount)
        {
            if (amount <= 0)
                return true;

            if (_resource < amount)
                return false;

            _resource -= amount;

            OnResourceChanged?.Invoke(_resource);

            return true;
        }

        public void Add(int amount)
        {
            Add(TeamType.Player, amount);
        }

        public void Add(TeamType team, int amount)
        {
            if (amount <= 0)
                return;

            if (team == TeamType.Enemy)
            {
                _enemyResource += amount;
                return;
            }

            _resource += amount;

            OnResourceChanged?.Invoke(_resource);
        }
    }
}
