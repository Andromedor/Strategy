using UnityEngine;
using UnityEngine.Serialization;

namespace Data
{
    [CreateAssetMenu(menuName = "RTS/Production Item")]
    public class ProductionItemData: ScriptableObject
    {
        [Header("Info")]
        public string ItemName;
        // Назва юніта/апгрейду.

        public Sprite Icon;
        // Іконка кнопки.

        [Header("Production")]
        public UnitData UnitData;
        // Якого юніта створюємо.

        public int Cost;
        // Вартість виробництва.

        [FormerlySerializedAs("BuildTime")] public float ProductionTime;
        // Час виробництва.
    }
}