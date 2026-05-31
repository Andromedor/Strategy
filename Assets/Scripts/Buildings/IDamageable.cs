using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Buildings
{
    public interface IDamageable
    {
        void TakeDamage(float damage);
    }
}