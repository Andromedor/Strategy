using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
   [SerializeField] private Camera _camera;
   [SerializeField] private LayerMask _buildingMask;
   
   public static BuildingProduction SelectedFactory;

   private void Update()
   {
      if (Mouse.current.leftButton.wasReleasedThisFrame)
      {
         SelectBuilding();
      }
   }

   private void SelectBuilding()
   {
      Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
      if (Physics.Raycast(ray, out var hit, 1000f, _buildingMask))
      {
         BuildingProduction production = hit.collider.GetComponent<BuildingProduction>();
         Factory factory = hit.collider.GetComponent<Factory>();

         if (production != null && factory != null)
         {
               SelectedFactory = production;
               EventManager.OnOpenPanel.Invoke(factory.PanelType);
         }
      }
      else
      {
         EventManager.OnOpenPanel.Invoke(PanelType.MainMenu);
      }
   }
}
