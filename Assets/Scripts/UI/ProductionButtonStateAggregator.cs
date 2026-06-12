using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Data;

namespace Strategy.UI
{
    public static class ProductionButtonStateAggregator
    {
        public static ProductionButtonRuntimeState Build(
            IReadOnlyList<BuildingProduction> factories,
            ProductionItemData item)
        {
            if (factories == null || item == null)
                return ProductionButtonRuntimeState.Empty;

            int pendingCount = 0;
            bool hasActiveProgress = false;
            FactoryProductionRuntimeState fastestState = default;

            for (int i = 0; i < factories.Count; i++)
            {
                BuildingProduction factory = factories[i];
                if (factory == null ||
                    !factory.isActiveAndEnabled ||
                    BuildingConstructionState.IsConstructing(factory))
                {
                    continue;
                }

                pendingCount += factory.CountPendingWorkFor(item);

                if (!factory.TryGetActiveProductionFor(item, out FactoryProductionRuntimeState state))
                    continue;

                if (hasActiveProgress && state.RemainingSeconds >= fastestState.RemainingSeconds)
                    continue;

                fastestState = state;
                hasActiveProgress = true;
            }

            return hasActiveProgress
                ? new ProductionButtonRuntimeState(
                    pendingCount,
                    true,
                    fastestState.Progress,
                    fastestState.RemainingSeconds)
                : new ProductionButtonRuntimeState(pendingCount, false, 0f, 0f);
        }
    }
}
