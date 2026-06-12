using System;
using Strategy.Core;
using UnityEngine;

namespace Strategy.Buildings
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class BuildingHealth : MonoBehaviour, IDamageable
    {
        public static readonly System.Collections.Generic.List<BuildingHealth> All = new();

        public event Action<BuildingHealth> HealthChanged;
        public event Action<BuildingHealth> Destroyed;

        [SerializeField, Min(1f)] private float _maxHealth = 1000f;
        [SerializeField] private bool _destroyOnDeath = true;

        private float _currentHealth;
        private float _constructionHealthCap;
        private bool _usesConstructionHealthCap;
        private bool _isDead;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public float NormalizedHealth => _maxHealth <= 0f ? 0f : Mathf.Clamp01(_currentHealth / _maxHealth);
        public bool IsDead => _isDead;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            All.Clear();
        }

        private void Awake()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _currentHealth = _maxHealth;
            _constructionHealthCap = _maxHealth;
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
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

            _currentHealth = Mathf.Min(CurrentHealthLimit, _currentHealth + amount);
            HealthChanged?.Invoke(this);
        }

        public void BeginConstructionHealth()
        {
            if (_isDead)
                return;

            _usesConstructionHealthCap = true;
            _constructionHealthCap = 1f;
            _currentHealth = 1f;
            HealthChanged?.Invoke(this);
        }

        public void UpdateConstructionHealth(float progress)
        {
            if (_isDead)
                return;

            float targetCap = Mathf.Lerp(1f, _maxHealth, Mathf.Clamp01(progress));
            ApplyConstructionHealthCap(targetCap, preserveDamage: true);
        }

        public void CompleteConstructionHealth()
        {
            if (_isDead || !_usesConstructionHealthCap)
                return;

            float previousCap = CurrentHealthLimit;
            float damageTaken = Mathf.Max(0f, previousCap - _currentHealth);

            _usesConstructionHealthCap = false;
            _constructionHealthCap = _maxHealth;
            _currentHealth = Mathf.Clamp(_maxHealth - damageTaken, 0f, _maxHealth);
            HealthChanged?.Invoke(this);

            if (_currentHealth <= 0f)
                Die();
        }

        public void SetCurrentHealthForLoad(float currentHealth)
        {
            _isDead = false;
            _usesConstructionHealthCap = false;
            _constructionHealthCap = _maxHealth;
            _currentHealth = Mathf.Clamp(currentHealth, 0f, _maxHealth);
            HealthChanged?.Invoke(this);

            if (_currentHealth <= 0f)
                Die();
        }

        /// <summary>
        /// Відновлює HP недобудованої будівлі: cap залежить від прогресу, але поточний HP лишається тим,
        /// що був у сейві, щоб атаки під час будівництва не стиралися.
        /// </summary>
        public void RestoreConstructionHealthForLoad(float currentHealth, float progress)
        {
            _isDead = false;
            _usesConstructionHealthCap = true;
            _constructionHealthCap = Mathf.Lerp(1f, _maxHealth, Mathf.Clamp01(progress));
            _currentHealth = Mathf.Clamp(currentHealth, 0f, CurrentHealthLimit);
            HealthChanged?.Invoke(this);

            if (_currentHealth <= 0f)
                Die();
        }

        private float CurrentHealthLimit => _usesConstructionHealthCap
            ? Mathf.Clamp(_constructionHealthCap, 1f, _maxHealth)
            : _maxHealth;

        private void ApplyConstructionHealthCap(float healthCap, bool preserveDamage)
        {
            float previousCap = CurrentHealthLimit;
            float damageTaken = preserveDamage ? Mathf.Max(0f, previousCap - _currentHealth) : 0f;

            _usesConstructionHealthCap = true;
            _constructionHealthCap = Mathf.Clamp(healthCap, 1f, _maxHealth);
            _currentHealth = Mathf.Clamp(_constructionHealthCap - damageTaken, 0f, _constructionHealthCap);
            HealthChanged?.Invoke(this);

            if (_currentHealth <= 0f)
                Die();
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
