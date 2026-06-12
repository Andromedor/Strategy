using Strategy.Save;
using UnityEngine;

namespace Strategy.Core
{
    public static class MatchLaunchContext
    {
        public static MatchLaunchConfig CurrentConfig { get; private set; }
        public static string PendingSavePath { get; private set; }

        public static bool HasConfig => CurrentConfig != null;
        public static bool HasPendingSaveLoad => !string.IsNullOrWhiteSpace(PendingSavePath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            CurrentConfig = null;
            PendingSavePath = null;
        }

        public static void SetConfig(MatchLaunchConfig config)
        {
            CurrentConfig = config;
            PendingSavePath = null;
        }

        public static void SetPendingSaveLoad(string savePath, MatchLaunchConfig config)
        {
            PendingSavePath = savePath;
            CurrentConfig = config;
        }

        public static void ClearPendingSaveLoad()
        {
            PendingSavePath = null;
        }

        public static void Clear()
        {
            CurrentConfig = null;
            PendingSavePath = null;
        }
    }
}
