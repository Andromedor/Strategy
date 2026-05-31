using Strategy.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    /// <summary>
    /// ScriptableObject, що визначає тип будівлі для розміщення — її префаб, економічну вартість,
    /// час побудови, здоров'я, колізійний бокс розміщення та який UI-панель відкривати при виборі.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingData", menuName = "RTS/BuildingData")]
    public class BuildingData : ScriptableObject
    {
        [Header("Info")]
        [SerializeField, FormerlySerializedAs("BuildingName")] private string _buildingName;
        [SerializeField, FormerlySerializedAs("Icon")] private Sprite _icon;

        [Header("Prefab")]
        [SerializeField, FormerlySerializedAs("prefab")] private GameObject _prefab;

        [Header("Economy")]
        [SerializeField, FormerlySerializedAs("economy")] private int _economyCost;

        [Header("Construction")]
        [SerializeField, FormerlySerializedAs("BuildTime")] private float _buildTime;
        [SerializeField, FormerlySerializedAs("MaxHealth")] private float _maxHealth;

        [Header("Placement")]
        // CheckBoxSize та CheckBoxOffset визначають бокс перетину, що використовується BuildingPlacementManager
        // для перевірки, чи місце розміщення вільне перед дозволом будівництва.
        [SerializeField, FormerlySerializedAs("CheckBoxSize")] private Vector3 _checkBoxSize;
        [SerializeField, FormerlySerializedAs("CheckBoxOffset")] private Vector3 _checkBoxOffset;

        [Header("UI")]
        [SerializeField, FormerlySerializedAs("PanelType")] private PanelType _panelType;

        public string BuildingName => _buildingName;
        public Sprite Icon => _icon;
        public GameObject Prefab => _prefab;
        public int EconomyCost => _economyCost;
        public float BuildTime => _buildTime;
        public float MaxHealth => _maxHealth;
        public Vector3 CheckBoxSize => _checkBoxSize;
        public Vector3 CheckBoxOffset => _checkBoxOffset;
        public PanelType PanelType => _panelType;
    }
}
