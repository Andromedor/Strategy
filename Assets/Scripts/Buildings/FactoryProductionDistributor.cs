using System.Collections.Generic;
using Strategy.Data;

namespace Strategy.Buildings
{
    public static class FactoryProductionDistributor
    {
        public static bool TryQueueLeastLoaded(
            IReadOnlyList<BuildingProduction> factories,
            ProductionItemData item,
            out BuildingProduction targetFactory)
        {
            targetFactory = ResolveLeastLoadedFactory(factories, item);
            if (targetFactory == null ||
                !targetFactory.TryResolveProductionItem(item, out ProductionItemData resolvedItem))
            {
                return false;
            }

            return targetFactory.AddToQueue(resolvedItem);
        }

        public static BuildingProduction ResolveLeastLoadedFactory(
            IReadOnlyList<BuildingProduction> factories,
            ProductionItemData item)
        {
            if (factories == null || item == null)
                return null;

            BuildingProduction bestFactory = null;
            int bestWorkCount = int.MaxValue;

            for (int i = 0; i < factories.Count; i++)
            {
                BuildingProduction factory = factories[i];
                if (factory == null ||
                    !factory.isActiveAndEnabled ||
                    BuildingConstructionState.IsConstructing(factory) ||
                    !factory.CanProduce(item))
                {
                    continue;
                }

                int workCount = factory.PendingWorkCount;
                if (bestFactory != null && workCount >= bestWorkCount)
                    continue;

                bestFactory = factory;
                bestWorkCount = workCount;
            }

            return bestFactory;
        }
    }
}
