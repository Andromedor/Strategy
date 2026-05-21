using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "RTS/Production Config")]
    public class ProductionConfig: ScriptableObject
    {
        public List<ProductionItemData> Items;
    }
}