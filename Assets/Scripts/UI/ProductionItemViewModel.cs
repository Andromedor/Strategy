using Strategy.Data;
using UnityEngine;

namespace Strategy.UI
{
    /// <summary>
    /// Immutable view-model for a single production queue item. Holds pre-formatted display strings
    /// and an affordability flag so UI components never access raw <see cref="ProductionItemData"/> directly.
    /// Create via the <see cref="From"/> factory method.
    /// </summary>
    public readonly struct ProductionItemViewModel
    {
        public ProductionItemViewModel(
            string displayName,
            Sprite icon,
            string costText,
            string timeText,
            string fallbackText,
            string tooltipText,
            bool isAffordable)
        {
            DisplayName = displayName;
            Icon = icon;
            CostText = costText;
            TimeText = timeText;
            FallbackText = fallbackText;
            TooltipText = tooltipText;
            IsAffordable = isAffordable;
        }

        public string DisplayName { get; }
        public Sprite Icon { get; }
        public string CostText { get; }
        public string TimeText { get; }
        public string FallbackText { get; }
        public string TooltipText { get; }
        public bool IsAffordable { get; }

        /// <summary>
        /// Constructs a fully populated view-model from a <see cref="ProductionItemData"/> asset,
        /// evaluating affordability against <paramref name="playerResource"/>.
        /// </summary>
        public static ProductionItemViewModel From(ProductionItemData item, int playerResource)
        {
            if (item == null)
            {
                return new ProductionItemViewModel(
                    "Unknown",
                    null,
                    "$0",
                    "0s",
                    "UNIT",
                    "Unknown",
                    false);
            }

            string displayName = FormatDisplayName(item.ItemName);
            string cost = FormatCost(item.Cost);
            string time = FormatSeconds(item.ProductionTime);

            return new ProductionItemViewModel(
                displayName,
                item.Icon,
                "$" + cost,
                time,
                "UNIT",
                BuildTooltipText(item, displayName, cost, time),
                item.Cost <= playerResource);
        }

        /// <summary>
        /// Composes the tooltip string, appending <see cref="UnitData"/> combat stats
        /// (health, damage, range, etc.) when the item has an associated unit.
        /// </summary>
        private static string BuildTooltipText(
            ProductionItemData item,
            string displayName,
            string cost,
            string time)
        {
            UnitData unit = item.UnitData;

            if (unit == null)
            {
                return
                    $"{displayName}\n" +
                    $"Cost: {cost}\n" +
                    $"Build time: {time}";
            }

            return
                $"{displayName}\n" +
                $"Cost: {cost}\n" +
                $"Build time: {time}\n\n" +
                "Stats\n" +
                $"Health: {FormatNumber(unit.MaxHealth)}\n" +
                $"Damage: {FormatNumber(unit.Damage)}\n" +
                $"Range: {FormatNumber(unit.AttackRange)}\n" +
                $"Attack delay: {FormatSeconds(unit.AttackDelay)}\n" +
                $"Speed: {FormatNumber(unit.Speed)}\n" +
                $"Formation: {FormatNumber(unit.FormationSpacing)}";
        }

        /// <summary>Converts internal asset name conventions to a readable display string.</summary>
        private static string FormatDisplayName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return "Unknown";

            return itemName
                .Replace("Meadl", "Medium")
                .Replace("LightTank", "Light Tank")
                .Replace("MediumTank", "Medium Tank");
        }

        private static string FormatCost(int cost)
        {
            return cost <= 0 ? "0" : cost.ToString();
        }

        private static string FormatSeconds(float seconds)
        {
            return $"{FormatNumber(seconds)}s";
        }

        /// <summary>Formats a float as an integer when whole, otherwise to two decimal places.</summary>
        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }
    }
}
