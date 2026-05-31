using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Core
{
    /// <summary>
    /// Статичний помічник, що ліниво створює та кешує іменовані дочірні GameObject під єдиним коренем "Runtime Objects"
    /// в ієрархії сцени. Використовується для впорядкування спавнених юнітів, будівель та UI-помічників під час виконання.
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

        /// <summary>Повертає трансформ іменованого контейнера під коренем, створюючи його, якщо він ще не існує.</summary>
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

        /// <summary>Повертає кешований кореневий трансформ, знаходячи або створюючи GameObject "Runtime Objects" у сцені за потреби.</summary>
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
