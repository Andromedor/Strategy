using System;
using UnityEngine;

namespace Strategy.AI
{
    public static class AiRuntimeSettings
    {
        public static event Action<bool> AiEnabledChanged;

        public static bool IsAiEnabled { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            IsAiEnabled = true;
            AiEnabledChanged = null;
        }

        public static void SetAllAiEnabled(bool enabled)
        {
            if (IsAiEnabled == enabled)
                return;

            IsAiEnabled = enabled;
            AiEnabledChanged?.Invoke(IsAiEnabled);
        }
    }
}
