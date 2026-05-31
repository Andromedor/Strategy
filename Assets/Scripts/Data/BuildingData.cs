using Strategy.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    /// <summary>
    /// ScriptableObject that defines a placeable building type — its prefab, economy cost,
    /// build time, health, placement collision box, and which UI panel to open when selected.
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
        // CheckBoxSize and CheckBoxOffset define the overlap-box used by BuildingPlacementManager
        // to test whether a placement location is clear before allowing construction.
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
