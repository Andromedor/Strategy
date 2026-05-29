using System;
using DefaultNamespace;
using UnitController;
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
         
         ConstructionCenter constructionCenter =
            hit.collider.GetComponentInParent<ConstructionCenter>();

         if (constructionCenter != null)
         {
            TeamComponent teamComponent = constructionCenter.GetComponentInParent<TeamComponent>();
            if (teamComponent != null && teamComponent.Team != TeamType.Player)
            {
               SelectedFactory = null;
               EventManager.OnConstructionClosed?.Invoke();
               EventManager.OnOpenPanel?.Invoke(PanelType.MainMenu);
               return;
            }

            SelectedFactory = null;
            
            EventManager.OnConstructionCenterSelected?.Invoke(constructionCenter);
            EventManager.OnOpenPanel?.Invoke(PanelType.Construction);

            return;
         }
         
         Outpost outpost = hit.collider.GetComponentInParent<Outpost>();

         if (outpost != null)
         {
            SelectedFactory = null;

            if (outpost.Owner != TeamType.Player)
            {
               EventManager.OnConstructionClosed?.Invoke();
               EventManager.OnOpenPanel?.Invoke(PanelType.MainMenu);
               return;
            }

            EventManager.OnOpenPanel?.Invoke(PanelType.Outpost);
            EventManager.OnOutpostSelected?.Invoke(outpost);
            return;
         }

         
         BuildingProduction production =
            hit.collider.GetComponentInParent<BuildingProduction>();

         if (production != null)
         {
              SelectedFactory = production;
              
              EventManager.OnOpenPanel?.Invoke(PanelType.Factory);
              EventManager.OnFactorySelected?.Invoke(production);
               return;
         }
      }
      
      SelectedFactory = null;
      EventManager.OnConstructionClosed?.Invoke();
      EventManager.OnOpenPanel?.Invoke(PanelType.MainMenu);
   }
}
