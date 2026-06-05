using Strategy.Data;
using UnityEngine;

namespace Strategy.Buildings
{
    public readonly struct FactoryProductionRuntimeState
    {
        public FactoryProductionRuntimeState(
            BuildingProduction sourceFactory,
            ProductionItemData item,
            float progress,
            float remainingSeconds,
            float durationSeconds)
        {
            SourceFactory = sourceFactory;
            Item = item;
            Progress = Mathf.Clamp01(progress);
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
            DurationSeconds = Mathf.Max(0f, durationSeconds);
        }

        public BuildingProduction SourceFactory { get; }
        public ProductionItemData Item { get; }
        public float Progress { get; }
        public float RemainingSeconds { get; }
        public float DurationSeconds { get; }
        public bool HasItem => Item != null;
    }
}
