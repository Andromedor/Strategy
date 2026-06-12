using Strategy.Buildings;
using Strategy.Core;
using Strategy.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
    /// <summary>
    /// Універсальна HUD-кнопка навігації, що відкриває цільовий <see cref="PanelType"/> через
    /// <see cref="EventManager.RaiseOpenPanel"/>. Може бути налаштована на вимогу наявності
    /// будівельного центру та/або заводу гравця перед тим, як стати інтерактивною.
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

        /// <summary>Генерує подію відкриття панелі для налаштованої цільової панелі.</summary>
        private void OpenPanel()
        {
            EventManager.RaiseOpenPanel(_targetPanel);
        }

        /// <summary>
        /// Повторно оцінює стан інтерактивності кнопки залежно від того, чи виконані
        /// умови наявності будівельного центру та/або заводу.
        /// </summary>
        private void Refresh()
        {
            if (_button == null)
                return;

            _button.interactable =
                (!_requiresConstructionCenter || HasPlayerConstructionCenter()) &&
                (!_requiresFactory || HasPlayerFactory());
        }

        /// <summary>Повертає true, якщо в сцені існує хоча б один активний будівельний центр команди гравця.</summary>
        private bool HasPlayerConstructionCenter()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (center != null && center.isActiveAndEnabled && BelongsToTeam(center))
                    return true;
            }

            return false;
        }

        /// <summary>Повертає true, якщо в сцені існує хоча б один активний завод команди гравця.</summary>
        private bool HasPlayerFactory()
        {
            foreach (BuildingProduction factory in BuildingProduction.All)
            {
                if (factory != null && factory.isActiveAndEnabled && BelongsToTeam(factory))
                    return true;
            }

            return false;
        }

        /// <summary>Повертає true, якщо <paramref name="component"/> належить налаштованій команді кнопки.</summary>
        private bool BelongsToTeam(Component component)
        {
            if (component is ConstructionCenter constructionCenter)
                return constructionCenter.BelongsToTeam(ResolveTeam());

            TeamComponent teamComponent = component.GetComponentInParent<TeamComponent>();
            return teamComponent != null && teamComponent.Team == ResolveTeam();
        }

        private TeamType ResolveTeam()
        {
            return _team == TeamType.Player
                ? LocalPlayerContext.LocalTeam
                : _team;
        }
    }
}
