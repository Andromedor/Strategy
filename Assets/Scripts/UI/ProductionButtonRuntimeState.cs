using UnityEngine;

namespace Strategy.UI
{
    public readonly struct ProductionButtonRuntimeState
    {
        public static ProductionButtonRuntimeState Empty => new ProductionButtonRuntimeState(0, false, 0f, 0f);

        public ProductionButtonRuntimeState(
            int pendingCount,
            bool hasActiveProgress,
            float progress,
            float remainingSeconds)
        {
            PendingCount = Mathf.Max(0, pendingCount);
            HasActiveProgress = hasActiveProgress;
            Progress = Mathf.Clamp01(progress);
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        }

        public int PendingCount { get; }
        public bool HasActiveProgress { get; }
        public float Progress { get; }
        public float RemainingSeconds { get; }
        public bool HasPendingWork => PendingCount > 0;
    }
}
