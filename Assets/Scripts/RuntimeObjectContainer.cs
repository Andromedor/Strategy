using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Core
{
    /// <summary>
    /// Static helper that lazily creates and caches named child GameObjects under a single "Runtime Objects"
    /// root in the scene hierarchy. Used to keep spawned units, buildings, and UI helpers organised at runtime.
    /// </summary>
    public static class RuntimeObjectContainer
    {
        private const string RootName = "Runtime Objects";

        private static Transform _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _root = null;
        }

        /// <summary>Returns the Transform of a named container under the root, creating it if it does not yet exist.</summary>
        public static Transform Get(string containerName)
        {
            Transform root = GetRoot();
            Transform container = root.Find(containerName);

            if (container != null)
                return container;

            GameObject containerObject = new GameObject(containerName);
            containerObject.transform.SetParent(root, false);
            return containerObject.transform;
        }

        /// <summary>Returns the cached root Transform, finding or creating the "Runtime Objects" GameObject in the scene if needed.</summary>
        private static Transform GetRoot()
        {
            if (_root != null)
                return _root;

            GameObject rootObject = GameObject.Find(RootName);

            if (rootObject == null)
                rootObject = new GameObject(RootName);

            _root = rootObject.transform;
            return _root;
        }
    }
}
