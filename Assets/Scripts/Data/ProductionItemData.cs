using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    /// <summary>
    /// ScriptableObject, що описує один запис виробленого юніта, відображуваного у UI заводу.
    /// Зберігає інформацію для відображення (назва, іконка), посилання на UnitData юніта, вартість ресурсів та час побудови.
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
        /// Записує всі поля запису виробництва за один виклик; використовується редакторними скриптами
        /// для встановлення еталонних вартостей та часу. Пропускає поле іконки, якщо воно null, щоб зберегти наявне зображення.
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
