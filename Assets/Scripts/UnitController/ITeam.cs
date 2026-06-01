using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Реалізується будь-яким об'єктом, що належить до команди (юніти, будівлі).
    /// Використовується UnitCombat, BulletController та ArtilleryProjectile для пропуску перевірок дружнього вогню.
    /// </summary>
    public interface ITeam
    {
        TeamType Team { get; }
    }
}
