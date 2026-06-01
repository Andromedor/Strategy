using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Індикатор дальності, специфічний для артилерії. Успадковує всю поведінку малювання кола від
    /// UnitAttackRangeIndicator без змін; існує як окремий тип, щоб артилерійні юніти могли отримати
    /// інший колір або стиль лінії через налаштування Інспектора за потреби.
    /// </summary>
    public class ArtilleryRangeIndicator : UnitAttackRangeIndicator
    {
    }
}
