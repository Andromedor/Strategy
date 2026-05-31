using Strategy.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.Buildings
{
    [RequireComponent(typeof(Button))]
    public class ButtonPlaceBuilding : MonoBehaviour
    {
        [SerializeField] private BuildingData _buildingData;
        [SerializeField] private Button _button;
        [SerializeField] private BuildingPlacementManager _placementManager;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (_button == null)
                return;

            _button.onClick.RemoveListener(PlaceBuilding);
            _button.onClick.AddListener(PlaceBuilding);
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(PlaceBuilding);
        }

        private void PlaceBuilding()
        {
            if (_placementManager == null || _buildingData == null)
                return;

            _placementManager.StartPlacement(_buildingData);
        }
    }
}
