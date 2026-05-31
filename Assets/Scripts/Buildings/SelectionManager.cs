using Strategy.Core;
using Strategy.UI;
using Strategy.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Strategy.Buildings
{
    /// <summary>
    /// Обробляє виділення будівель лівою кнопкою миші (заводи, центри будівництва, аванпости).
    /// Викидає відповідні події EventManager та відкриває відповідну панель HUD.
    /// </summary>
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

        /// <summary>Кидає промінь від курсора миші та направляє до відповідного обробника вибору залежно від знайденого компонента.</summary>
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

        /// <summary>Відкриває панель будівництва для ConstructionCenter гравця; знімає виділення, якщо він належить ворогу.</summary>
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

        /// <summary>Відкриває панель аванпосту, якщо він належить гравцю; інакше знімає виділення.</summary>
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

        /// <summary>Знімає виділення з активного заводу, викидає ConstructionClosed та повертає HUD до панелі головного меню.</summary>
        private static void ClearBuildingSelection()
        {
            SelectedFactory = null;
            EventManager.RaiseConstructionClosed();
            EventManager.RaiseOpenPanel(PanelType.MainMenu);
        }
    }
}
