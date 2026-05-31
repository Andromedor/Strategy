using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    /// <summary>
    /// ScriptableObject describing a single producible unit entry shown in the factory UI.
    /// Holds display info (name, icon), a reference to the unit's UnitData, resource cost, and build time.
    /// </summary>
    [CreateAssetMenu(menuName = "RTS/Production Item")]
    public class ProductionItemData : ScriptableObject
    {
        [Header("Info")]
        [SerializeField, FormerlySerializedAs("ItemName")] private string _itemName;
        [SerializeField, FormerlySerializedAs("Icon")] private Sprite _icon;

        [Header("Production")]
        [SerializeField, FormerlySerializedAs("UnitData")] private UnitData _unitData;
        [SerializeField, FormerlySerializedAs("Cost")] private int _cost;
        [SerializeField, FormerlySerializedAs("BuildTime")]
        [FormerlySerializedAs("ProductionTime")]
        private float _productionTime;

        public string ItemName => _itemName;
        public Sprite Icon => _icon;
        public UnitData UnitData => _unitData;
        public int Cost => _cost;
        public float ProductionTime => _productionTime;

        /// <summary>
        /// Writes all production item fields at once; used by editor builder scripts to stamp
        /// canonical cost and time values. Skips the icon field when null to preserve existing art.
        /// </summary>
        public void Configure(string itemName, UnitData unitData, int cost, float productionTime, Sprite icon = null)
        {
            _itemName = itemName;
            _unitData = unitData;
            _cost = cost;
            _productionTime = productionTime;

            if (icon != null)
                _icon = icon;
        }
    }
}
