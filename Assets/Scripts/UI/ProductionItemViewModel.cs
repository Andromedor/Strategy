using Strategy.Data;
using UnityEngine;

namespace Strategy.UI
{
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

        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }
    }
}
