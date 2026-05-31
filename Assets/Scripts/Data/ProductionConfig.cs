using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    /// <summary>
    /// ScriptableObject, що є головним списком усіх юнітів, які може виробляти завод.
    /// Асет "Factory Production Config.asset" є єдиним екземпляром, що використовується під час гри компонентом BuildingProduction.
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
        /// Додає запис ProductionItemData до списку, якщо він не null і ще не присутній.
        /// Викликається редакторними конфігураторами префабів для реєстрації щойно створених даних виробництва юніта.
        /// </summary>
        public void AddItem(ProductionItemData item)
        {
            _items ??= new List<ProductionItemData>();

            if (item != null && !_items.Contains(item))
                _items.Add(item);
        }
    }
}
