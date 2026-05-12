using System;
using UnityEngine;
using UnityEngine.UI;

public class ButtonPlaceBuilding : MonoBehaviour
{
  [SerializeField] private GameObject prefab;
  [SerializeField] private Button _button;

  private void Start()
  {
    _button.onClick.AddListener(PlaceBuilding);
  }

  private void OnDisable()
  {
    _button.onClick.RemoveAllListeners();
  }

  public void PlaceBuilding()
  {
    Instantiate(prefab, Vector3.zero, Quaternion.identity);
    Debug.Log("Place Building");
  }
}
