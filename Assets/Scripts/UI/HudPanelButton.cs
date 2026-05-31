using Strategy.Buildings;
using Strategy.Core;
using Strategy.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
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

        private void OpenPanel()
        {
            EventManager.RaiseOpenPanel(_targetPanel);
        }

        private void Refresh()
        {
            if (_button == null)
                return;

            _button.interactable =
                (!_requiresConstructionCenter || HasPlayerConstructionCenter()) &&
                (!_requiresFactory || HasPlayerFactory());
        }

        private bool HasPlayerConstructionCenter()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (center != null && center.isActiveAndEnabled && BelongsToTeam(center))
                    return true;
            }

            return false;
        }

        private bool HasPlayerFactory()
        {
            foreach (BuildingProduction factory in BuildingProduction.All)
            {
                if (factory != null && factory.isActiveAndEnabled && BelongsToTeam(factory))
                    return true;
            }

            return false;
        }

        private bool BelongsToTeam(Component component)
        {
            TeamComponent teamComponent = component.GetComponentInParent<TeamComponent>();
            return teamComponent == null || teamComponent.Team == _team;
        }
    }
}
