using UnityEngine;

namespace Building_and_creat_Uniit
{
    public class UnitHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private UnitData _unitData;
        
        private float _currentHealth;
        
        private void Awake()
        {
            _currentHealth = _unitData.MaxHealth;
        }

        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;

            if (_currentHealth <= 0f)
            {
                EventManager.OnUnitDeselected?.Invoke(gameObject);
                Destroy(gameObject);
            }
        }
    }
}