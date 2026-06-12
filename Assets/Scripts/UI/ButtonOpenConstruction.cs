using Strategy.Buildings;
using Strategy.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
    /// <summary>
    /// HUD-кнопка, що відкриває панель будівництва через <see cref="EventManager.RaiseOpenPanel"/>.
    /// Автоматично вимикається, коли жоден <see cref="ConstructionCenter"/> не є активним у сцені.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonOpenConstruction : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(OpenConstruction);

            EventManager.OnConstructionCentersChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OpenConstruction);

            EventManager.OnConstructionCentersChanged -= Refresh;
        }

        /// <summary>Генерує подію відкриття панелі для перемикання HUD на панель будівництва.</summary>
        private void OpenConstruction()
        {
            EventManager.RaiseOpenPanel(PanelType.Construction);
        }

        /// <summary>Вмикає кнопку лише тоді, коли існує хоча б один активний будівельний центр.</summary>
        private void Refresh()
        {
            if (_button != null)
                _button.interactable = HasPlayerConstructionCenter();
        }

        private static bool HasPlayerConstructionCenter()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (center != null &&
                    center.isActiveAndEnabled &&
                    center.BelongsToTeam(LocalPlayerContext.LocalTeam))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
