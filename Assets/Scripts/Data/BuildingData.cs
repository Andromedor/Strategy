using Strategy.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.Data
{
    public enum BuildingGridPivot
    {
        Center,
        BottomLeft,
        BottomCenter,
        BottomRight,
        CenterLeft,
        CenterRight,
        TopLeft,
        TopCenter,
        TopRight
    }

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
        [SerializeField] private Vector2Int _gridFootprintCells = Vector2Int.one;
        [SerializeField] private BuildingGridPivot _gridPivot = BuildingGridPivot.Center;
        [SerializeField] private bool _autoCalculateFootprintFromCheckBox;
        [SerializeField, Min(0.01f)] private float _autoCalculateCellSize = 5f;

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
        public Vector2Int GridFootprintCells => ClampFootprint(_gridFootprintCells);
        public BuildingGridPivot GridPivot => _gridPivot;
        public bool AutoCalculateFootprintFromCheckBox => _autoCalculateFootprintFromCheckBox;
        public PanelType PanelType => _panelType;

        public Vector2Int ResolveGridFootprint(float cellSize)
        {
            Vector2Int footprint = ClampFootprint(_gridFootprintCells);

            if (footprint.x > 0 && footprint.y > 0)
                return footprint;

            return CalculateGridFootprint(cellSize);
        }

        public Vector2Int CalculateGridFootprint(float cellSize)
        {
            float safeCellSize = Mathf.Max(0.01f, cellSize);
            int x = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(_checkBoxSize.x) / safeCellSize));
            int y = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(_checkBoxSize.z) / safeCellSize));
            return new Vector2Int(x, y);
        }

        private void OnValidate()
        {
            _gridFootprintCells = ClampFootprint(_gridFootprintCells);
            _autoCalculateCellSize = Mathf.Max(0.01f, _autoCalculateCellSize);

            if (_autoCalculateFootprintFromCheckBox)
                _gridFootprintCells = CalculateGridFootprint(_autoCalculateCellSize);
        }

        private static Vector2Int ClampFootprint(Vector2Int footprint)
        {
            return new Vector2Int(
                Mathf.Max(1, footprint.x),
                Mathf.Max(1, footprint.y));
        }
    }
}
