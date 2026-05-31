using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Загальний інтерфейс для будь-якого ігрового об'єкта, що може отримувати пошкодження.
    /// Реалізується юнітами та будівлями, які беруть участь у бойових діях.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Застосовує вказану кількість пошкоджень до об'єкта, що реалізує інтерфейс.</summary>
        void TakeDamage(float damage);
    }
}
