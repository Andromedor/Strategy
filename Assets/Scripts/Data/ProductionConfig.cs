using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
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

        public void AddItem(ProductionItemData item)
        {
            _items ??= new List<ProductionItemData>();

            if (item != null && !_items.Contains(item))
                _items.Add(item);
        }
    }
}
