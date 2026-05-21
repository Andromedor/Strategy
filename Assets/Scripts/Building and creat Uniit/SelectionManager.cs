using System;
using UnityEngine;
using UnityEngine.EventSystems;
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
         if (BuildingPlacementManager.IsPlacing)
            return;
         
         if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
         
         SelectBuilding();
      }
   }

   private void SelectBuilding()
   {
      Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
      if (Physics.Raycast(ray, out var hit, 1000f, _buildingMask))
      {
         BuildingProduction production =
            hit.collider.GetComponent<BuildingProduction>();

         if (production != null)
         {
              SelectedFactory = production;
              
              EventManager.OnOpenPanel?.Invoke(PanelType.Factory);
              EventManager.OnFactorySelected?.Invoke(production);
               return;
         }
      }
      
      SelectedFactory = null;
      EventManager.OnOpenPanel?.Invoke(PanelType.MainMenu);
   }
}
