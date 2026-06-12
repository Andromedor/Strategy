using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Звичайний C# клас (не MonoBehaviour), що відстежує очки здоров'я юніта.
    /// Створюється в UnitCombat.Awake зі значенням MaxHealth юніта з UnitData.
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
        /// Віднімає шкоду від поточного здоров'я, обмежуючи результат мінімумом нуля.
        /// </summary>
        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;

            if (_currentHealth < 0f)
                _currentHealth = 0f;
        }

        public void SetCurrentHealth(float value, float maxHealth)
        {
            _currentHealth = Mathf.Clamp(value, 0f, Mathf.Max(0f, maxHealth));
        }
    }
}
