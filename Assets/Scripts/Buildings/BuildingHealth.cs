using System;
using Strategy.Core;
using UnityEngine;

namespace Strategy.Buildings
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class BuildingHealth : MonoBehaviour, IDamageable
    {
        public event Action<BuildingHealth> HealthChanged;
        public event Action<BuildingHealth> Destroyed;

        [SerializeField, Min(1f)] private float _maxHealth = 1000f;
        [SerializeField] private bool _destroyOnDeath = true;

        private float _currentHealth;
        private bool _isDead;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public float NormalizedHealth => _maxHealth <= 0f ? 0f : Mathf.Clamp01(_currentHealth / _maxHealth);
        public bool IsDead => _isDead;

        private void Awake()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _currentHealth = _maxHealth;
        }

        public void TakeDamage(float damage)
        {
            if (_isDead || damage <= 0f)
                return;

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            HealthChanged?.Invoke(this);

            if (_currentHealth <= 0f)
                Die();
        }

        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f)
                return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            HealthChanged?.Invoke(this);
        }

        private void Die()
        {
            if (_isDead)
                return;

            _isDead = true;
            Destroyed?.Invoke(this);
            EventManager.RaiseBuildingDestroyed(gameObject);
            EventManager.RaiseBuildingDeselected(gameObject);

            if (TryGetComponent(out ConstructionCenter constructionCenter))
            {
                constructionCenter.HideBuildArea();
                EventManager.RaiseConstructionClosed();
            }

            if (_destroyOnDeath)
                Destroy(gameObject);
        }
    }
}
