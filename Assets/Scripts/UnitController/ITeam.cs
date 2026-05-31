using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Implemented by any object that belongs to a team (units, buildings).
    /// Used by UnitCombat, BulletController, and ArtilleryProjectile to skip friendly-fire checks.
    /// </summary>
    public interface ITeam
    {
        TeamType Team { get; }
    }
}
