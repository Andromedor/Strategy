using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Common interface for any game object that can receive damage.
    /// Implemented by units and buildings that participate in combat.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Applies the given damage amount to the implementing object.</summary>
        void TakeDamage(float damage);
    }
}