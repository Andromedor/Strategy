using System;
using UnityEngine;
using UnityEngine.UI;

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
    _button.onClick.RemoveListener(PlaceBuilding);
    _button.onClick.AddListener(PlaceBuilding);
  }
  
  private void OnDisable()
  {
    _button.onClick.RemoveAllListeners();
  }


  private void PlaceBuilding()
  {
    _placementManager.StartPlacement(_buildingData);
    Debug.Log("Place Building");
  }
}
