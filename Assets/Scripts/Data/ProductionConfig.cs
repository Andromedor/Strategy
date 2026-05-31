using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    /// <summary>
    /// ScriptableObject acting as the master list of all units a factory can produce.
    /// The "Factory Production Config.asset" is the single instance used at runtime by BuildingProduction.
    /// </summary>
    [CreateAssetMenu(menuName = "RTS/Production Config")]
    public class ProductionConfig : ScriptableObject
    {
        [SerializeField, FormerlySerializedAs("Items")]
        private List<ProductionItemData> _items = new();

        public IReadOnlyList<ProductionItemData> Items => _items;

        private void OnEnable()
        {
            _items ??= new List<ProductionItemData>();
        }

        /// <summary>
        /// Adds a ProductionItemData entry to the list if it is not null and not already present.
        /// Called by editor prefab builders to register newly created unit production data.
        /// </summary>
        public void AddItem(ProductionItemData item)
        {
            _items ??= new List<ProductionItemData>();

            if (item != null && !_items.Contains(item))
                _items.Add(item);
        }
    }
}
