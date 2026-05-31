using Strategy.Core;
using Strategy.UI;
using Strategy.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Strategy.Buildings
{
    public class SelectionManager : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera _camera;
        [SerializeField] private LayerMask _buildingMask;

        public static BuildingProduction SelectedFactory { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SelectedFactory = null;
        }

        private void Awake()
        {
            if (_camera == null)
                _camera = UnityEngine.Camera.main;
        }

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasReleasedThisFrame)
                return;

            if (BuildingPlacementManager.IsPlacing)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            SelectBuilding();
        }

        private void SelectBuilding()
        {
            if (_camera == null)
                _camera = UnityEngine.Camera.main;

            if (_camera == null || Mouse.current == null)
                return;

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _buildingMask))
            {
                ClearBuildingSelection();
                return;
            }

            ConstructionCenter constructionCenter = hit.collider.GetComponentInParent<ConstructionCenter>();
            if (constructionCenter != null)
            {
                SelectConstructionCenter(constructionCenter);
                return;
            }

            Outpost outpost = hit.collider.GetComponentInParent<Outpost>();
            if (outpost != null)
            {
                SelectOutpost(outpost);
                return;
            }

            BuildingProduction production = hit.collider.GetComponentInParent<BuildingProduction>();
            if (production != null)
            {
                SelectedFactory = production;
                EventManager.RaiseOpenPanel(PanelType.Factory);
                EventManager.RaiseFactorySelected(production);
                return;
            }

            ClearBuildingSelection();
        }

        private static void SelectConstructionCenter(ConstructionCenter constructionCenter)
        {
            TeamComponent teamComponent = constructionCenter.GetComponentInParent<TeamComponent>();
            if (teamComponent != null && teamComponent.Team != TeamType.Player)
            {
                ClearBuildingSelection();
                return;
            }

            SelectedFactory = null;
            EventManager.RaiseConstructionCenterSelected(constructionCenter);
            EventManager.RaiseOpenPanel(PanelType.Construction);
        }

        private static void SelectOutpost(Outpost outpost)
        {
            SelectedFactory = null;

            if (outpost.Owner != TeamType.Player)
            {
                ClearBuildingSelection();
                return;
            }

            EventManager.RaiseOpenPanel(PanelType.Outpost);
            EventManager.RaiseOutpostSelected(outpost);
        }

        private static void ClearBuildingSelection()
        {
            SelectedFactory = null;
            EventManager.RaiseConstructionClosed();
            EventManager.RaiseOpenPanel(PanelType.MainMenu);
        }
    }
}
