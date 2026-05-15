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
         Factory factory = hit.collider.GetComponentInParent<Factory>();

         if (factory != null)
         {
            var production = factory.GetComponent<BuildingProduction>();
            if(production != null)
              SelectedFactory = production;
            
            EventManager.OnOpenPanel?.Invoke(factory.PanelType);
               return;
         }
      }
      
      SelectedFactory = null;
      EventManager.OnOpenPanel?.Invoke(PanelType.MainMenu);
   }
}
