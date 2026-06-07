using UnityEngine;

namespace Strategy.Data
{
    [CreateAssetMenu(fileName = "BuildingPlacementGridConfig", menuName = "RTS/Building Placement Grid Config")]
    public class BuildingPlacementGridConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _cellSize = 5f;
        [SerializeField] private Vector3 _origin;
        [SerializeField] private GameObject _markerPrefab;
        [SerializeField] private Color _validCellColor = new(0f, 0.95f, 1f, 0.42f);
        [SerializeField] private Color _invalidCellColor = new(1f, 0.05f, 0.05f, 0.48f);
        [SerializeField] private Color _buildAreaCellColor = new(0f, 0.85f, 1f, 0.16f);
        [SerializeField, Min(0f)] private float _markerYOffset = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float _markerScale = 0.94f;

        public float CellSize => Mathf.Max(0.01f, _cellSize);
        public Vector3 Origin => _origin;
        public GameObject MarkerPrefab => _markerPrefab;
        public Color ValidCellColor => _validCellColor;
        public Color InvalidCellColor => _invalidCellColor;
        public Color BuildAreaCellColor => _buildAreaCellColor;
        public float MarkerYOffset => _markerYOffset;
        public float MarkerScale => _markerScale;
    }
}
