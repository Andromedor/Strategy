using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Plain C# class (not a MonoBehaviour) that tracks hit points for a unit.
    /// Created by UnitCombat.Awake with the unit's MaxHealth value from UnitData.
    /// </summary>
    public class UnitHealth
    {
        private float _currentHealth;

        public float CurrentHealth => _currentHealth;

        public bool IsDead => _currentHealth <= 0f;

        public UnitHealth(float maxHealth)
        {
            _currentHealth = maxHealth;
        }

        /// <summary>
        /// Subtracts damage from current health, clamping the result to a minimum of zero.
        /// </summary>
        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;

            if (_currentHealth < 0f)
                _currentHealth = 0f;
        }
    }
}
