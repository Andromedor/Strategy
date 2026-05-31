using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Buildings
{
    public class UnitHealth 
    {
        private float _currentHealth;
        // Поточна кількість HP.

        public float CurrentHealth => _currentHealth;
        // Публічне читання HP.

        public bool IsDead => _currentHealth <= 0f;
        // Чи мертвий юніт.

        
        public UnitHealth(float maxHealth)
        {
            _currentHealth = maxHealth;
        }

        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;

            if (_currentHealth < 0f)
                _currentHealth = 0f;
        }
    }
}