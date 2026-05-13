using System;
using UnityEngine;
using UnityEngine.UI;

public class ButtonPlaceBuilding : MonoBehaviour
{
  [SerializeField] private GameObject prefab;
  [SerializeField] private Button _button;

  private void Awake()
  {
    if (_button == null)
      _button = GetComponent<Button>();
  }

  private void OnDisable()
  {
    _button.onClick.RemoveAllListeners();
  }
  
  private void OnEnable()
  {
    _button.onClick.RemoveListener(PlaceBuilding);
    _button.onClick.AddListener(PlaceBuilding);
  }

  private void PlaceBuilding()
  {
    if (PlaceObject.IsPlacing)
      return;
    
    Instantiate(prefab, Vector3.zero, Quaternion.identity);
    EventManager.OnOpenPanel?.Invoke(PanelType.MainMenu);
    
    Debug.Log("Place Building");
  }
}
