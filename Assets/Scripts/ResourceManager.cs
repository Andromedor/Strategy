using System;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    /// <summary>
    /// Singleton that tracks separate resource pools for the player and enemy.
    /// Outposts add resources via Add(); units and buildings spend them via Spend().
    /// Fires OnResourceChanged whenever the player's pool changes.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public static event Action<int> OnResourceChanged;

        [SerializeField] private int _startResource = 500;
        [SerializeField] private int _startEnemyResource = 500;

        private int _resource;
        private int _enemyResource;

        public int Resource => _resource;
        public int EnemyResource => _enemyResource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
            OnResourceChanged = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _resource = _startResource;
            _enemyResource = _startEnemyResource;

            OnResourceChanged?.Invoke(_resource);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Deducts amount from the player's resource pool if affordable; fires OnResourceChanged and returns true on success.</summary>
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

        /// <summary>Convenience overload that adds resources to the player's pool.</summary>
        public void Add(int amount)
        {
            Add(TeamType.Player, amount);
        }

        /// <summary>Adds resources to the specified team's pool; fires OnResourceChanged only for the player team.</summary>
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
