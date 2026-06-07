using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Strategy.Units
{
    public static class UnitHealthBarVisibility
    {
        public static event Action<bool> ForceVisibilityChanged;

        private static int _lastPolledFrame = -1;

        public static bool ForceVisible { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ForceVisible = false;
            _lastPolledFrame = -1;
            ForceVisibilityChanged = null;
        }

        public static void PollKeyboard()
        {
            if (Time.frameCount == _lastPolledFrame)
                return;

            _lastPolledFrame = Time.frameCount;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.altKey.wasPressedThisFrame)
                return;

            SetForceVisible(!ForceVisible);
        }

        public static void SetForceVisible(bool visible)
        {
            if (ForceVisible == visible)
                return;

            ForceVisible = visible;
            ForceVisibilityChanged?.Invoke(ForceVisible);
        }
    }
}
