using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Artillery-specific range indicator. Inherits all circle-drawing behaviour from
    /// UnitAttackRangeIndicator without modification; exists as a distinct type so artillery
    /// units can be given a different color or line style via Inspector overrides if needed.
    /// </summary>
    public class ArtilleryRangeIndicator : UnitAttackRangeIndicator
    {
    }
}
