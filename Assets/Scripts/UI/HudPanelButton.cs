using Strategy.Buildings;
using Strategy.Core;
using Strategy.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
    /// <summary>
    /// General-purpose HUD navigation button that opens a target <see cref="PanelType"/> via
    /// <see cref="EventManager.RaiseOpenPanel"/>. Can be configured to require a player
    /// construction center and/or factory before becoming interactable.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HudPanelButton : MonoBehaviour
    {
        [SerializeField] private PanelType _targetPanel = PanelType.MainMenu;
        [SerializeField] private bool _requiresConstructionCenter;
        [SerializeField] private bool _requiresFactory;
        [SerializeField] private TeamType _team = TeamType.Player;
        [SerializeField] private Button _button;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(OpenPanel);

            EventManager.OnConstructionCentersChanged += Refresh;
            BuildingProduction.FactoriesChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OpenPanel);

            EventManager.OnConstructionCentersChanged -= Refresh;
            BuildingProduction.FactoriesChanged -= Refresh;
        }

        /// <summary>Raises the open-panel event for the configured target panel.</summary>
        private void OpenPanel()
        {
            EventManager.RaiseOpenPanel(_targetPanel);
        }

        /// <summary>
        /// Re-evaluates the button's interactable state based on whether the required
        /// construction center and/or factory conditions are met.
        /// </summary>
        private void Refresh()
        {
            if (_button == null)
                return;

            _button.interactable =
                (!_requiresConstructionCenter || HasPlayerConstructionCenter()) &&
                (!_requiresFactory || HasPlayerFactory());
        }

        /// <summary>Returns true if at least one active, player-team construction center exists in the scene.</summary>
        private bool HasPlayerConstructionCenter()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (center != null && center.isActiveAndEnabled && BelongsToTeam(center))
                    return true;
            }

            return false;
        }

        /// <summary>Returns true if at least one active, player-team factory exists in the scene.</summary>
        private bool HasPlayerFactory()
        {
            foreach (BuildingProduction factory in BuildingProduction.All)
            {
                if (factory != null && factory.isActiveAndEnabled && BelongsToTeam(factory))
                    return true;
            }

            return false;
        }

        /// <summary>Returns true if <paramref name="component"/> is owned by the button's configured team.</summary>
        private bool BelongsToTeam(Component component)
        {
            TeamComponent teamComponent = component.GetComponentInParent<TeamComponent>();
            return teamComponent == null || teamComponent.Team == _team;
        }
    }
}
