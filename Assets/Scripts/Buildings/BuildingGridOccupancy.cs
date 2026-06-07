using Strategy.Data;
using UnityEngine;

namespace Strategy.Buildings
{
    public class BuildingGridOccupancy : MonoBehaviour
    {
        [SerializeField] private BuildingData _buildingData;
        [SerializeField] private BuildingPlacementGridConfig _gridConfig;
        [SerializeField] private Vector2Int _originCell;
        [SerializeField] private int _rotationSteps;
        [SerializeField] private bool _reserveOnEnable = true;
        [SerializeField] private bool _snapTransformToGridOnEnable = true;

        public Vector2Int OriginCell => _originCell;
        public int RotationSteps => BuildingGridPlacementService.NormalizeRotationSteps(_rotationSteps);

        public void Initialize(
            BuildingData buildingData,
            BuildingPlacementGridConfig gridConfig,
            Vector2Int originCell,
            int rotationSteps)
        {
            BuildingGridPlacementService.Release(gameObject);

            _buildingData = buildingData;
            _gridConfig = gridConfig;
            _originCell = originCell;
            _rotationSteps = BuildingGridPlacementService.NormalizeRotationSteps(rotationSteps);

            if (isActiveAndEnabled)
                BuildingGridPlacementService.Reserve(_buildingData, _originCell, _rotationSteps, _gridConfig, gameObject);
        }

        private void OnEnable()
        {
            if (!_reserveOnEnable || _buildingData == null)
                return;

            if (_gridConfig != null)
            {
                _originCell = BuildingGridPlacementService.WorldToPlacementOriginCell(
                    _buildingData,
                    transform.position,
                    _rotationSteps,
                    _gridConfig);

                if (_snapTransformToGridOnEnable)
                {
                    Vector3 snappedPosition = BuildingGridPlacementService.GetPlacementPosition(
                        _buildingData,
                        _originCell,
                        _rotationSteps,
                        _gridConfig);
                    transform.position = new Vector3(snappedPosition.x, transform.position.y, snappedPosition.z);
                }
            }

            BuildingGridPlacementService.Reserve(_buildingData, _originCell, _rotationSteps, _gridConfig, gameObject);
        }

        private void OnDisable()
        {
            BuildingGridPlacementService.Release(gameObject);
        }

        private void OnDestroy()
        {
            BuildingGridPlacementService.Release(gameObject);
        }
    }
}
